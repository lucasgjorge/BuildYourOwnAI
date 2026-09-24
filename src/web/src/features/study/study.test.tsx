import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { renderApp } from '../../test/render'
import { deferred, loggedIn, problem, server } from '../../test/server'

const organization = {
  id: 'o1', name: 'Curso', createdAt: '2026-09-24T10:00:00Z', documentCount: 2,
  assistants: [{ id: 'a1', name: 'Professor', routingDescription: 'explica' }],
}
const documents = [
  { id: 'd1', fileName: 'apostila-1.pdf', sizeBytes: 10, chunkCount: 4, uploadedAt: '2026-09-24T10:00:00Z' },
  { id: 'd2', fileName: 'apostila-2.pdf', sizeBytes: 10, chunkCount: 4, uploadedAt: '2026-09-24T10:00:00Z' },
]
const questions = [1, 2, 3].map(n => ({
  id: `q${n}`, position: n, prompt: `Pergunta ${n}?`, options: [`A${n}`, `B${n}`, `C${n}`, `D${n}`],
}))
// q1: right is B1 (1); q2: right is C2 (2); q3: right is A3 (0).
const key: Record<string, number> = { q1: 1, q2: 2, q3: 0 }

function studyApi(options: { documents?: typeof documents; create?: () => Promise<Response> | Response } = {}) {
  const created: unknown[] = []
  const chunksRequested: string[] = []
  return {
    created,
    chunksRequested,
    handlers: [
      loggedIn(),
      http.get('*/api/organizations/o1', () => HttpResponse.json(organization)),
      http.get('*/api/organizations/o1/documents', () => HttpResponse.json(options.documents ?? documents)),
      http.post('*/api/organizations/o1/study-sessions', async ({ request }) => {
        created.push(await request.json())
        if (options.create) return options.create()
        return HttpResponse.json({ id: 's1', createdAt: '2026-09-24T10:00:00Z', questions }, { status: 201 })
      }),
      http.post('*/api/study-sessions/s1/questions/:q/answer', async ({ params, request }) => {
        const { option } = await request.json() as { option: number }
        const q = String(params.q)
        return HttpResponse.json({
          correct: option === key[q], chosenOption: option, correctOption: key[q], explanation: `Porque sim ${q}.`,
          source: { documentId: 'd2', fileName: 'apostila-2.pdf', chunkIndex: 3 },
        })
      }),
      http.get('*/api/organizations/o1/documents/:doc/chunks/:index', ({ params }) => {
        chunksRequested.push(`${params.doc}/${params.index}`)
        return HttpResponse.json({
          documentId: params.doc, fileName: 'apostila-2.pdf', chunkCount: 4,
          chunks: [{ index: Number(params.index), content: `trecho de origem ${params.index}` }],
        })
      }),
    ],
  }
}

async function start(user: ReturnType<typeof renderApp>['user']) {
  await user.click(await screen.findByRole('button', { name: 'Começar' }))
  await screen.findByText('Pergunta 1 de 3')
}

const option = (text: string) => screen.getByRole('button', { name: text })

describe('study mode', () => {
  // C22
  it('organization has conversa, estudar and base tabs', async () => {
    server.use(...studyApi().handlers)

    renderApp('/organizations/o1')

    const tabs = await screen.findByRole('navigation', { name: 'Seções da organização' })
    expect(within(tabs).getAllByRole('link').map(l => l.textContent)).toEqual(['Conversa', 'Estudar', 'Base'])
    expect(within(tabs).getByRole('link', { name: 'Estudar' })).toHaveAttribute('href', '/organizations/o1/study')
  })

  // C23
  it('study setup sends chosen documents and count', async () => {
    const api = studyApi()
    server.use(...api.handlers)
    const { user } = renderApp('/organizations/o1/study')

    const first = await screen.findByRole('checkbox', { name: 'apostila-1.pdf' })
    expect(first).toBeChecked()
    expect(screen.getByRole('checkbox', { name: 'apostila-2.pdf' })).toBeChecked()
    expect(screen.getByRole('radio', { name: '10' })).toBeChecked()
    expect(screen.getByRole('radio', { name: '5' })).not.toBeChecked()
    expect(screen.getByRole('radio', { name: '20' })).not.toBeChecked()

    await user.click(first)
    await user.click(screen.getByRole('radio', { name: '5' }))
    await user.click(screen.getByRole('button', { name: 'Começar' }))

    await waitFor(() => expect(api.created).toEqual([{ documentIds: ['d2'], questionCount: 5 }]))
  })

  // C24
  it('study without documents points to base', async () => {
    server.use(...studyApi({ documents: [] }).handlers)

    renderApp('/organizations/o1/study')

    expect(await screen.findByText('Suba documentos na Base para estudar')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Abrir a Base' })).toHaveAttribute('href', '/organizations/o1/knowledge')
    expect(screen.queryByRole('button', { name: 'Começar' })).not.toBeInTheDocument()
  })

  // C25
  it('study shows generating state', async () => {
    const { gate, release } = deferred()
    server.use(...studyApi({
      create: async () => {
        await gate
        return HttpResponse.json({ id: 's1', createdAt: '2026-09-24T10:00:00Z', questions }, { status: 201 })
      },
    }).handlers)
    const { user } = renderApp('/organizations/o1/study')

    await user.click(await screen.findByRole('button', { name: 'Começar' }))

    expect(await screen.findByText('Gerando perguntas…')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Começar' })).toBeDisabled()
    release()
    expect(await screen.findByText('Pergunta 1 de 3')).toBeInTheDocument()
  })

  // C26
  it('study shows one question at a time', async () => {
    server.use(...studyApi().handlers)
    const { user } = renderApp('/organizations/o1/study')

    await start(user)

    expect(screen.getByRole('heading', { name: 'Pergunta 1?' })).toBeInTheDocument()
    expect(screen.queryByText('Pergunta 2?')).not.toBeInTheDocument()
    for (const text of ['A1', 'B1', 'C1', 'D1']) expect(option(text)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Responder' })).toBeDisabled()
    await user.click(option('C1'))
    expect(screen.getByRole('button', { name: 'Responder' })).toBeEnabled()
  })

  // C27
  it('right answer shows certo and optional preview', async () => {
    const api = studyApi()
    server.use(...api.handlers)
    const { user } = renderApp('/organizations/o1/study')

    await start(user)
    await user.click(option('B1'))
    await user.click(screen.getByRole('button', { name: 'Responder' }))

    expect(await screen.findByText('Certo!')).toBeInTheDocument()
    expect(option('B1')).toHaveAttribute('data-state', 'correct')
    expect(screen.getByText('Porque sim q1.')).toBeInTheDocument()
    expect(screen.queryByRole('complementary', { name: 'Prévia do trecho' })).not.toBeInTheDocument()
    expect(api.chunksRequested).toEqual([])

    await user.click(screen.getByRole('button', { name: 'Ver no documento' }))
    const panel = await screen.findByRole('complementary', { name: 'Prévia do trecho' })
    expect(await within(panel).findByText('trecho de origem 3')).toBeInTheDocument()
    expect(api.chunksRequested).toEqual(['d2/3'])
  })

  // C28
  it('wrong answer shows the right one with the preview', async () => {
    const api = studyApi()
    server.use(...api.handlers)
    const { user } = renderApp('/organizations/o1/study')

    await start(user)
    await user.click(option('D1'))
    await user.click(screen.getByRole('button', { name: 'Responder' }))

    expect(await screen.findByText('A resposta certa é: B1')).toBeInTheDocument()
    expect(option('D1')).toHaveAttribute('data-state', 'wrong')
    expect(option('B1')).toHaveAttribute('data-state', 'correct')
    expect(screen.getByText('Porque sim q1.')).toBeInTheDocument()
    const panel = await screen.findByRole('complementary', { name: 'Prévia do trecho' })
    expect(await within(panel).findByText('trecho de origem 3')).toHaveAttribute('aria-current', 'true')
    expect(api.chunksRequested).toEqual(['d2/3'])
  })

  // C29
  it('study moves through questions to the result', async () => {
    server.use(...studyApi().handlers)
    const { user } = renderApp('/organizations/o1/study')

    await start(user)
    await user.click(option('D1'))
    await user.click(screen.getByRole('button', { name: 'Responder' }))
    await screen.findByRole('complementary', { name: 'Prévia do trecho' })
    await user.click(await screen.findByRole('button', { name: 'Próxima pergunta' }))

    expect(await screen.findByText('Pergunta 2 de 3')).toBeInTheDocument()
    expect(screen.queryByRole('complementary', { name: 'Prévia do trecho' })).not.toBeInTheDocument()
    await user.click(option('C2'))
    await user.click(screen.getByRole('button', { name: 'Responder' }))
    await user.click(await screen.findByRole('button', { name: 'Próxima pergunta' }))
    await screen.findByText('Pergunta 3 de 3')
    await user.click(option('B3'))
    await user.click(screen.getByRole('button', { name: 'Responder' }))
    await user.click(await screen.findByRole('button', { name: 'Ver resultado' }))

    expect(await screen.findByText('Você acertou 1 de 3')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Estudar de novo' }))
    expect(await screen.findByRole('button', { name: 'Começar' })).toBeInTheDocument()
  })

  // C30
  it('study errors show title and keep choices', async () => {
    const api = studyApi({ create: () => problem(502, 'Não foi possível gerar perguntas. Tente novamente.') })
    server.use(...api.handlers)
    const { user } = renderApp('/organizations/o1/study')

    await user.click(await screen.findByRole('checkbox', { name: 'apostila-2.pdf' }))
    await user.click(screen.getByRole('radio', { name: '20' }))
    await user.click(screen.getByRole('button', { name: 'Começar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível gerar perguntas. Tente novamente.')
    expect(screen.getByRole('checkbox', { name: 'apostila-2.pdf' })).not.toBeChecked()
    expect(screen.getByRole('radio', { name: '20' })).toBeChecked()

    server.use(
      http.post('*/api/organizations/o1/study-sessions', () =>
        HttpResponse.json({ id: 's1', createdAt: '2026-09-24T10:00:00Z', questions }, { status: 201 })),
      http.post('*/api/study-sessions/s1/questions/:q/answer', () => problem(409, 'Esta pergunta já foi respondida.')),
    )
    await user.click(screen.getByRole('button', { name: 'Começar' }))
    await screen.findByText('Pergunta 1 de 3')
    await user.click(option('A1'))
    await user.click(screen.getByRole('button', { name: 'Responder' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Esta pergunta já foi respondida.')
  })
})
