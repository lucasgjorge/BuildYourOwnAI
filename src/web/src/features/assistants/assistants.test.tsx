import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { renderApp } from '../../test/render'
import { assistant, assistantPageHandlers, deferred, loggedIn, problem, server } from '../../test/server'

describe('assistants list', () => {
  // C30
  it('shows empty state', async () => {
    server.use(loggedIn(), http.get('*/api/assistants', () => HttpResponse.json([])))

    renderApp('/assistants')

    expect(await screen.findByText('Você ainda não criou nenhuma IA')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Criar IA' })).toBeInTheDocument()
  })

  // C33
  it('shows problem title and keeps input (create assistant)', async () => {
    server.use(
      loggedIn(),
      http.get('*/api/assistants', () => HttpResponse.json([])),
      http.post('*/api/assistants', () => problem(400, 'Nome inválido')),
    )
    const { user } = renderApp('/assistants')

    await user.type(await screen.findByLabelText('Nome'), 'Minha IA')
    await user.click(screen.getByRole('button', { name: 'Criar IA' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Nome inválido')
    expect(screen.getByLabelText('Nome')).toHaveValue('Minha IA')
  })

  // C34
  it('delete asks for confirmation (assistant)', async () => {
    const deletes: string[] = []
    server.use(
      loggedIn(),
      http.get('*/api/assistants', () => HttpResponse.json([assistant])),
      http.delete('*/api/assistants/:id', ({ params }) => {
        deletes.push(String(params.id))
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const confirm = vi.fn().mockReturnValueOnce(false).mockReturnValueOnce(true)
    window.confirm = confirm
    const { user } = renderApp('/assistants')
    const button = await screen.findByRole('button', { name: 'Apagar IA Suporte' })

    await user.click(button)
    expect(confirm).toHaveBeenCalledTimes(1)
    expect(deletes).toEqual([])

    await user.click(button)
    expect(confirm).toHaveBeenCalledTimes(2)
    await waitFor(() => expect(deletes).toEqual(['a1']))
  })
})

describe('assistant page', () => {
  // C31
  it('assistant page shows documents upload and question', async () => {
    server.use(...assistantPageHandlers())

    renderApp('/assistants/a1')

    expect(await screen.findByText('politicas.pdf')).toBeInTheDocument()
    expect(screen.getByText('manual.txt')).toBeInTheDocument()
    expect(screen.getByLabelText(/Arquivo/)).toHaveAttribute('type', 'file')
    expect(screen.getByRole('button', { name: 'Enviar' })).toBeInTheDocument()
    expect(screen.getByLabelText('Pergunta')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Perguntar' })).toBeInTheDocument()
  })

  // C32
  it('shows answer and source file names', async () => {
    server.use(
      ...assistantPageHandlers(),
      http.post('*/api/assistants/a1/ask', () => HttpResponse.json({
        answer: 'O prazo é de 30 dias.',
        sources: [
          { documentId: 'd2', fileName: 'politicas.pdf', chunkIndex: 0, excerpt: 'prazo de 30 dias' },
          { documentId: 'd9', fileName: 'contrato.md', chunkIndex: 3, excerpt: 'trinta dias' },
        ],
      })),
    )
    const { user } = renderApp('/assistants/a1')

    await user.type(await screen.findByLabelText('Pergunta'), 'Qual o prazo?')
    await user.click(screen.getByRole('button', { name: 'Perguntar' }))

    const answer = (await screen.findByText('O prazo é de 30 dias.')).closest('article')!
    expect(within(answer).getByText('politicas.pdf')).toBeInTheDocument()
    expect(within(answer).getByText('contrato.md')).toBeInTheDocument()
  })

  // C33
  it('shows problem title and keeps input (ask)', async () => {
    server.use(
      ...assistantPageHandlers(),
      http.post('*/api/assistants/a1/ask', () => problem(502, 'O provedor de IA falhou.')),
    )
    const { user } = renderApp('/assistants/a1')

    await user.type(await screen.findByLabelText('Pergunta'), 'Qual o prazo?')
    await user.click(screen.getByRole('button', { name: 'Perguntar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('O provedor de IA falhou.')
    expect(screen.getByLabelText('Pergunta')).toHaveValue('Qual o prazo?')
  })

  // C34
  it('delete asks for confirmation (document)', async () => {
    const deletes: string[] = []
    server.use(
      ...assistantPageHandlers(),
      http.delete('*/api/assistants/a1/documents/:documentId', ({ params }) => {
        deletes.push(String(params.documentId))
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const confirm = vi.fn().mockReturnValueOnce(false).mockReturnValueOnce(true)
    window.confirm = confirm
    const { user } = renderApp('/assistants/a1')
    const button = await screen.findByRole('button', { name: 'Apagar documento manual.txt' })

    await user.click(button)
    expect(confirm).toHaveBeenCalledTimes(1)
    expect(deletes).toEqual([])

    await user.click(button)
    await waitFor(() => expect(deletes).toEqual(['d1']))
  })

  // C35
  it('disables button while processing (upload)', async () => {
    const { gate, release } = deferred()
    server.use(
      ...assistantPageHandlers(),
      http.post('*/api/assistants/a1/documents', async () => {
        await gate
        return HttpResponse.json({ id: 'd3', fileName: 'novo.txt', sizeBytes: 5, chunkCount: 1, uploadedAt: '2026-09-23T11:00:00Z' }, { status: 201 })
      }),
    )
    const { user } = renderApp('/assistants/a1')

    await user.upload(await screen.findByLabelText(/Arquivo/), new File(['texto'], 'novo.txt', { type: 'text/plain' }))
    await user.click(screen.getByRole('button', { name: 'Enviar' }))

    const busy = await screen.findByRole('button', { name: 'Processando...' })
    expect(busy).toBeDisabled()
    release()
    expect(await screen.findByRole('button', { name: 'Enviar' })).toBeEnabled()
  })

  // C35
  it('disables button while processing (ask)', async () => {
    const { gate, release } = deferred()
    server.use(
      ...assistantPageHandlers(),
      http.post('*/api/assistants/a1/ask', async () => {
        await gate
        return HttpResponse.json({ answer: 'ok', sources: [] })
      }),
    )
    const { user } = renderApp('/assistants/a1')

    await user.type(await screen.findByLabelText('Pergunta'), 'Qual o prazo?')
    await user.click(screen.getByRole('button', { name: 'Perguntar' }))

    const busy = await screen.findByRole('button', { name: 'Processando...' })
    expect(busy).toBeDisabled()
    release()
    expect(await screen.findByRole('button', { name: 'Perguntar' })).toBeEnabled()
  })
})
