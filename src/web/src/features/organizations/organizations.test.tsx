import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { renderApp } from '../../test/render'
import {
  assistantPageHandlers,
  deferred,
  loggedIn,
  organization,
  organizationPageHandlers,
  organizationSummary,
  problem,
  server,
} from '../../test/server'

describe('organizations list', () => {
  // C56 (rag-mvp C30)
  it('organizations empty state and create', async () => {
    server.use(
      loggedIn(),
      http.get('*/api/organizations', () => HttpResponse.json([])),
      http.post('*/api/organizations', () =>
        HttpResponse.json({ id: 'o1', name: 'ACME', createdAt: '2026-09-23T10:00:00Z' }, { status: 201 })),
      ...organizationPageHandlers().slice(1),
    )
    const { user } = renderApp('/organizations')

    expect(await screen.findByText('Crie sua primeira organização')).toBeInTheDocument()
    await user.type(screen.getByLabelText('Nome da organização'), 'ACME')
    await user.click(screen.getByRole('button', { name: 'Criar organização' }))

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/organizations/o1'))
  })

  // rag-mvp C33 (create)
  it('shows problem title and keeps input (create organization)', async () => {
    server.use(
      loggedIn(),
      http.get('*/api/organizations', () => HttpResponse.json([])),
      http.post('*/api/organizations', () => problem(400, 'Nome inválido')),
    )
    const { user } = renderApp('/organizations')

    await user.type(await screen.findByLabelText('Nome da organização'), 'ACME')
    await user.click(screen.getByRole('button', { name: 'Criar organização' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Nome inválido')
    expect(screen.getByLabelText('Nome da organização')).toHaveValue('ACME')
  })

  // C58 (rag-mvp C34)
  it('organization delete confirms', async () => {
    const deletes: string[] = []
    server.use(
      loggedIn(),
      http.get('*/api/organizations', () => HttpResponse.json([organizationSummary])),
      http.delete('*/api/organizations/:id', ({ params }) => {
        deletes.push(String(params.id))
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const confirm = vi.fn().mockReturnValueOnce(false).mockReturnValueOnce(true)
    window.confirm = confirm
    const { user } = renderApp('/organizations')
    const button = await screen.findByRole('button', { name: 'Apagar organização ACME' })

    await user.click(button)
    expect(confirm).toHaveBeenCalledTimes(1)
    expect(confirm.mock.calls[0][0]).toMatch(/IAs, os documentos e as lacunas/)
    expect(deletes).toEqual([])

    await user.click(button)
    await waitFor(() => expect(deletes).toEqual(['o1']))
  })

  // rag-mvp C47
  it('organizations list shows loading', async () => {
    const { gate, release } = deferred()
    server.use(loggedIn(), http.get('*/api/organizations', async () => {
      await gate
      return HttpResponse.json([organizationSummary])
    }))

    renderApp('/organizations')

    expect(await screen.findByRole('heading', { name: 'Organizações' })).toBeInTheDocument()
    expect(screen.getByText('Carregando...')).toBeInTheDocument()
    release()
    expect(await screen.findByRole('link', { name: 'ACME' })).toBeInTheDocument()
    expect(screen.queryByText('Carregando...')).not.toBeInTheDocument()
  })

  // rag-mvp C51
  it('create button shows processing', async () => {
    const { gate, release } = deferred()
    server.use(
      loggedIn(),
      http.get('*/api/organizations', () => HttpResponse.json([])),
      http.post('*/api/organizations', async () => {
        await gate
        return problem(400, 'Nome inválido')
      }),
    )
    const { user } = renderApp('/organizations')

    await user.type(await screen.findByLabelText('Nome da organização'), 'ACME')
    await user.click(screen.getByRole('button', { name: 'Criar organização' }))

    expect(await screen.findByRole('button', { name: 'Processando...' })).toBeDisabled()
    release()
    expect(await screen.findByRole('button', { name: 'Criar organização' })).toBeEnabled()
  })
})

describe('organization page', () => {
  // C57 (rag-mvp C31)
  it('organization page lists documents and assistants', async () => {
    const created: unknown[] = []
    server.use(
      ...organizationPageHandlers(),
      http.post('*/api/assistants', async ({ request }) => {
        created.push(await request.json())
        return HttpResponse.json({ id: 'a3' }, { status: 201 })
      }),
    )
    const { user } = renderApp('/organizations/o1')

    expect(await screen.findByText('politicas.pdf')).toBeInTheDocument()
    expect(screen.getByText('manual.txt')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Direto' })).toHaveAttribute('href', '/assistants/a1')
    expect(screen.getByRole('link', { name: 'Professor' })).toBeInTheDocument()
    expect(screen.getByLabelText(/Arquivo/)).toHaveAttribute('type', 'file')
    expect(screen.getByRole('button', { name: 'Enviar' })).toBeInTheDocument()

    await user.type(screen.getByLabelText('Nome'), 'Resumo')
    await user.type(screen.getByLabelText('Quando usar esta IA'), 'quando a pessoa quer um resumo')
    await user.click(screen.getByRole('button', { name: 'Criar IA' }))

    await waitFor(() => expect(created).toEqual([
      { organizationId: 'o1', name: 'Resumo', instructions: null, routingDescription: 'quando a pessoa quer um resumo' },
    ]))
  })

  // C59
  it('organization page empty, loading and not found', async () => {
    const { gate, release } = deferred()
    server.use(
      loggedIn(),
      http.get('*/api/organizations/o1', async () => {
        await gate
        return HttpResponse.json({ ...organization, assistants: [] })
      }),
      http.get('*/api/organizations/o1/documents', () => HttpResponse.json([])),
      http.get('*/api/organizations/o9', () => problem(404, 'Organização não encontrada.')),
    )

    const first = renderApp('/organizations/o1')
    // The nav only renders after the session guard resolved, so the loader seen now is the page's own.
    expect(await screen.findByRole('link', { name: 'Organizações' })).toBeInTheDocument()
    expect(screen.getByText('Carregando...')).toBeInTheDocument()
    release()
    expect(await screen.findByText('Nenhuma IA nesta organização')).toBeInTheDocument()
    first.unmount()

    renderApp('/organizations/o9')
    expect(await screen.findByRole('alert')).toHaveTextContent('Organização não encontrada.')
  })

  // rag-mvp C34 (document)
  it('delete asks for confirmation (document)', async () => {
    const deletes: string[] = []
    server.use(
      ...organizationPageHandlers(),
      http.delete('*/api/organizations/o1/documents/:documentId', ({ params }) => {
        deletes.push(String(params.documentId))
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const confirm = vi.fn().mockReturnValueOnce(false).mockReturnValueOnce(true)
    window.confirm = confirm
    const { user } = renderApp('/organizations/o1')
    const button = await screen.findByRole('button', { name: 'Apagar documento manual.txt' })

    await user.click(button)
    expect(deletes).toEqual([])
    await user.click(button)
    await waitFor(() => expect(deletes).toEqual(['d1']))
  })

  // rag-mvp C34 (assistant)
  it('delete asks for confirmation (assistant)', async () => {
    const deletes: string[] = []
    server.use(
      ...organizationPageHandlers(),
      http.delete('*/api/assistants/:id', ({ params }) => {
        deletes.push(String(params.id))
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const confirm = vi.fn().mockReturnValueOnce(false).mockReturnValueOnce(true)
    window.confirm = confirm
    const { user } = renderApp('/organizations/o1')
    const button = await screen.findByRole('button', { name: 'Apagar IA Direto' })

    await user.click(button)
    expect(deletes).toEqual([])
    await user.click(button)
    await waitFor(() => expect(deletes).toEqual(['a1']))
  })

  // rag-mvp C35 (upload)
  it('disables button while processing (upload)', async () => {
    const { gate, release } = deferred()
    server.use(
      ...organizationPageHandlers(),
      http.post('*/api/organizations/o1/documents', async () => {
        await gate
        return HttpResponse.json({ id: 'd3', fileName: 'novo.txt', sizeBytes: 5, chunkCount: 1, uploadedAt: '2026-09-23T11:00:00Z' }, { status: 201 })
      }),
    )
    const { user } = renderApp('/organizations/o1')

    await user.upload(await screen.findByLabelText(/Arquivo/), new File(['texto'], 'novo.txt', { type: 'text/plain' }))
    await user.click(screen.getByRole('button', { name: 'Enviar' }))

    expect(await screen.findByRole('button', { name: 'Processando...' })).toBeDisabled()
    release()
    expect(await screen.findByRole('button', { name: 'Enviar' })).toBeEnabled()
  })

  // rag-mvp C48
  it('empty document list shows only upload', async () => {
    server.use(
      loggedIn(),
      http.get('*/api/organizations/o1', () => HttpResponse.json(organization)),
      http.get('*/api/organizations/o1/documents', () => HttpResponse.json([])),
    )

    renderApp('/organizations/o1')

    const upload = await screen.findByLabelText(/Arquivo/)
    const section = upload.closest('section')!
    await waitFor(() => expect(within(section).queryByText('Carregando...')).not.toBeInTheDocument())
    expect(within(section).queryAllByRole('listitem')).toHaveLength(0)
    expect(within(section).getByRole('button', { name: 'Enviar' })).toBeInTheDocument()
  })

  // rag-mvp C49
  it('upload error shows problem title', async () => {
    server.use(
      ...organizationPageHandlers(),
      http.post('*/api/organizations/o1/documents', () => problem(415, 'Formato não suportado. Use .pdf, .txt, .md.')),
    )
    const { user } = renderApp('/organizations/o1')

    await user.upload(await screen.findByLabelText(/Arquivo/), new File(['x'], 'nota.txt', { type: 'text/plain' }))
    await user.click(screen.getByRole('button', { name: 'Enviar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Formato não suportado. Use .pdf, .txt, .md.')
  })
})

describe('assistant page', () => {
  // C60 (rag-mvp C32)
  it('assistant page asks and links to organization', async () => {
    server.use(
      ...assistantPageHandlers(),
      http.post('*/api/assistants/a1/ask', () => HttpResponse.json({
        answer: 'O prazo é de 30 dias.',
        found: true,
        sources: [
          { documentId: 'd2', fileName: 'politicas.pdf', chunkIndex: 0, excerpt: 'prazo de 30 dias' },
          { documentId: 'd9', fileName: 'contrato.md', chunkIndex: 3, excerpt: 'trinta dias' },
        ],
      })),
    )
    const { user } = renderApp('/assistants/a1')

    expect(await screen.findByRole('link', { name: '← ACME' })).toHaveAttribute('href', '/organizations/o1')
    expect(screen.queryByLabelText(/Arquivo/)).not.toBeInTheDocument()
    await user.type(screen.getByLabelText('Pergunta'), 'Qual o prazo?')
    await user.click(screen.getByRole('button', { name: 'Perguntar' }))

    const answer = (await screen.findByText('O prazo é de 30 dias.')).closest('article')!
    expect(within(answer).getByText('politicas.pdf')).toBeInTheDocument()
    expect(within(answer).getByText('contrato.md')).toBeInTheDocument()
  })

  // rag-mvp C33 (ask)
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

  // rag-mvp C35 (ask)
  it('disables button while processing (ask)', async () => {
    const { gate, release } = deferred()
    server.use(
      ...assistantPageHandlers(),
      http.post('*/api/assistants/a1/ask', async () => {
        await gate
        return HttpResponse.json({ answer: 'ok', found: true, sources: [] })
      }),
    )
    const { user } = renderApp('/assistants/a1')

    await user.type(await screen.findByLabelText('Pergunta'), 'Qual o prazo?')
    await user.click(screen.getByRole('button', { name: 'Perguntar' }))

    expect(await screen.findByRole('button', { name: 'Processando...' })).toBeDisabled()
    release()
    expect(await screen.findByRole('button', { name: 'Perguntar' })).toBeEnabled()
  })
})

describe('session guard (rag-mvp C50)', () => {
  it('session guard states: loading while the session is unknown', async () => {
    const { gate, release } = deferred()
    server.use(
      http.get('*/api/auth/manage/info', async () => {
        await gate
        return HttpResponse.json({ email: 'ana@test.local', isEmailConfirmed: false })
      }),
      http.get('*/api/organizations', () => HttpResponse.json([])),
    )

    renderApp('/organizations')

    expect(await screen.findByText('Carregando...')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Organizações' })).not.toBeInTheDocument()
    release()
    expect(await screen.findByRole('heading', { name: 'Organizações' })).toBeInTheDocument()
  })

  it('session guard states: a non-401 failure shows the problem title and stays', async () => {
    server.use(http.get('*/api/auth/manage/info', () => problem(500, 'Servidor indisponível')))

    renderApp('/organizations')

    expect(await screen.findByRole('alert')).toHaveTextContent('Servidor indisponível')
    expect(screen.getByTestId('location')).toHaveTextContent('/organizations')
  })
})
