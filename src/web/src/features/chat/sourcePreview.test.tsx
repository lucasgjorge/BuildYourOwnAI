import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { renderApp } from '../../test/render'
import { deferred, loggedIn, problem, server } from '../../test/server'

const nexora = {
  id: 'o1', name: 'Nexora Tech', createdAt: '2026-09-24T10:00:00Z', documentCount: 2,
  assistants: [
    { id: 'rh', name: 'RH', routingDescription: 'processos de RH' },
    { id: 'culture', name: 'Culture', routingDescription: 'cultura da empresa' },
  ],
}
const rh = { id: 'rh', name: 'RH', organizationName: 'Nexora Tech' }

// 5 sources from 2 documents: 3 chunks of 01_rh.txt, 2 of 02_cultura.txt.
const sources = [
  { documentId: 'd1', fileName: '01_rh.txt', chunkIndex: 2, excerpt: 'a' },
  { documentId: 'd1', fileName: '01_rh.txt', chunkIndex: 0, excerpt: 'b' },
  { documentId: 'd2', fileName: '02_cultura.txt', chunkIndex: 4, excerpt: 'c' },
  { documentId: 'd1', fileName: '01_rh.txt', chunkIndex: 5, excerpt: 'd' },
  { documentId: 'd2', fileName: '02_cultura.txt', chunkIndex: 1, excerpt: 'e' },
]

const chunk = (index: number) => ({ index, content: `conteúdo do trecho ${index}` })

function api(options: { found?: boolean; chunks?: (doc: string, index: number) => Promise<Response> | Response } = {}) {
  const requested: string[] = []
  return {
    requested,
    handlers: [
      loggedIn(),
      http.get('*/api/organizations', () => HttpResponse.json([{ id: 'o1', name: 'Nexora Tech', createdAt: nexora.createdAt, assistantCount: 2, documentCount: 2 }])),
      http.get('*/api/organizations/o1', () => HttpResponse.json(nexora)),
      http.post('*/api/organizations/o1/route/ask', () => HttpResponse.json({
        kind: 'answered', assistant: rh, answer: 'Pelo portal.', found: options.found ?? true, sources, alternatives: [],
      })),
      http.get('*/api/organizations/o1/documents/:doc/chunks/:index', ({ params }) => {
        const doc = String(params.doc)
        const index = Number(params.index)
        requested.push(`${doc}/${index}`)
        if (options.chunks) return options.chunks(doc, index)
        return HttpResponse.json({
          documentId: doc, fileName: doc === 'd1' ? '01_rh.txt' : '02_cultura.txt', chunkCount: 8,
          chunks: [index - 1, index, index + 1].filter(i => i >= 0 && i < 8).map(chunk),
        })
      }),
    ],
  }
}

async function ask(user: ReturnType<typeof renderApp>['user']) {
  await user.type(await screen.findByLabelText('Mensagem'), 'Como peço férias?')
  await user.click(screen.getByRole('button', { name: 'Enviar' }))
}

const preview = () => screen.getByRole('complementary', { name: 'Prévia do trecho' })

describe('source preview', () => {
  // C11
  it('sources are grouped by document and the first chunk opens', async () => {
    const { handlers, requested } = api()
    server.use(...handlers)
    const { user } = renderApp('/organizations/o1')

    await ask(user)

    const answer = (await screen.findByText('Pelo portal.')).closest('article')!
    expect(within(answer).getAllByText('01_rh.txt')).toHaveLength(1)
    expect(within(answer).getAllByText('02_cultura.txt')).toHaveLength(1)
    const rhGroup = within(answer).getByText('01_rh.txt').closest('li')!
    const cultureGroup = within(answer).getByText('02_cultura.txt').closest('li')!
    expect(within(rhGroup).getAllByRole('button').map(b => b.textContent)).toEqual(['trecho 3', 'trecho 1', 'trecho 6'])
    expect(within(cultureGroup).getAllByRole('button').map(b => b.textContent)).toEqual(['trecho 5', 'trecho 2'])
    await waitFor(() => expect(requested).toEqual(['d1/2']))
    expect(await within(preview()).findByText('Trecho 3 de 8')).toBeInTheDocument()
  })

  // C12
  it('preview shows the chunk with its neighbors', async () => {
    const { handlers, requested } = api()
    server.use(...handlers)
    const { user } = renderApp('/organizations/o1')

    await ask(user)
    const cultureGroup = (await screen.findByText('02_cultura.txt')).closest('li')!
    await user.click(within(cultureGroup).getByRole('button', { name: 'trecho 5' }))

    await waitFor(() => expect(requested).toContain('d2/4'))
    const panel = preview()
    expect(await within(panel).findByText('Trecho 5 de 8')).toBeInTheDocument()
    expect(within(panel).getByRole('heading', { name: '02_cultura.txt' })).toBeInTheDocument()
    expect(within(panel).getByText('conteúdo do trecho 4')).toHaveAttribute('aria-current', 'true')
    expect(within(panel).getByText('conteúdo do trecho 3')).not.toHaveAttribute('aria-current')
    expect(within(panel).getByText('conteúdo do trecho 5')).not.toHaveAttribute('aria-current')
    expect(within(cultureGroup).getByRole('button', { name: 'trecho 5' })).toHaveAttribute('aria-pressed', 'true')
  })

  // C13
  it('not found answer shows no sources and no preview', async () => {
    const { handlers, requested } = api({ found: false })
    server.use(...handlers)
    const { user } = renderApp('/organizations/o1')

    await ask(user)

    const answer = (await screen.findByText('Pelo portal.')).closest('article')!
    expect(within(answer).queryByText('Fontes')).not.toBeInTheDocument()
    expect(within(answer).queryByRole('button', { name: /trecho/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('complementary', { name: 'Prévia do trecho' })).not.toBeInTheDocument()
    expect(requested).toEqual([])
  })

  // C14
  it('preview closes and reopens', async () => {
    const { handlers } = api()
    server.use(...handlers)
    const { user } = renderApp('/organizations/o1')

    await ask(user)
    await within(await screen.findByRole('complementary', { name: 'Prévia do trecho' })).findByText('Trecho 3 de 8')
    await user.click(screen.getByRole('button', { name: 'Fechar prévia' }))
    expect(screen.queryByRole('complementary', { name: 'Prévia do trecho' })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'trecho 1' }))
    expect(await within(preview()).findByText('Trecho 1 de 8')).toBeInTheDocument()
  })

  // C15
  it('preview loading and error states', async () => {
    const { gate, release } = deferred()
    const { handlers } = api({
      chunks: async (_doc, index) => {
        if (index === 2) {
          await gate
          return HttpResponse.json({ documentId: 'd1', fileName: '01_rh.txt', chunkCount: 8, chunks: [chunk(2)] })
        }
        return problem(404, 'Trecho não encontrado.')
      },
    })
    server.use(...handlers)
    const { user } = renderApp('/organizations/o1')

    await ask(user)
    expect(await within(await screen.findByRole('complementary', { name: 'Prévia do trecho' })).findByText('Carregando trecho…')).toBeInTheDocument()
    release()
    expect(await within(preview()).findByText('Trecho 3 de 8')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'trecho 6' }))
    expect(await within(preview()).findByRole('alert')).toHaveTextContent('Trecho não encontrado.')
  })

  // C16
  it('preview overlays on narrow screens', async () => {
    const { handlers } = api()
    server.use(...handlers)
    const { user } = renderApp('/organizations/o1')

    await ask(user)

    const panel = await screen.findByRole('complementary', { name: 'Prévia do trecho' })
    for (const cls of ['fixed', 'inset-0', 'lg:sticky'] as const) expect(panel.className.split(/\s+/)).toContain(cls)
    expect(within(panel).getByRole('button', { name: 'Fechar prévia' })).toBeInTheDocument()
  })
})

describe('automatic choice', () => {
  // C9
  it('automatic choice wording', async () => {
    const { gate, release } = deferred()
    const { handlers } = api()
    // MSW uses the first matching handler, so the gated answer goes first.
    server.use(http.post('*/api/organizations/o1/route/ask', async () => {
      await gate
      return HttpResponse.json({ kind: 'answered', assistant: rh, answer: 'Pelo portal.', found: true, sources, alternatives: [] })
    }), ...handlers)
    const { user } = renderApp('/organizations/o1')

    expect(await screen.findByRole('radio', { name: 'Escolha automática' })).toBeChecked()
    const nav = screen.getByRole('navigation', { name: 'Principal' })
    expect(within(nav).getByRole('link', { name: 'Todas as IAs' })).toHaveAttribute('href', '/all')
    await ask(user)
    expect(await screen.findByText('Escolhendo quem responde…')).toBeInTheDocument()
    release()
    const answer = (await screen.findByText('Pelo portal.')).closest('article')!
    expect(within(answer).getByText('escolha automática')).toBeInTheDocument()
  })

  // C17
  it('chat never scrolls sideways', async () => {
    const scrollIntoView = vi.fn()
    Element.prototype.scrollIntoView = scrollIntoView
    const scrollTo = vi.fn()
    window.scrollTo = scrollTo as unknown as typeof window.scrollTo
    // A found answer: the preview opens and centers its chunk, the case where sideways scroll used to come back.
    const { handlers } = api()
    server.use(...handlers)
    const { user } = renderApp('/organizations/o1')

    await ask(user)
    await screen.findByText('Pelo portal.')
    expect(await within(await screen.findByRole('complementary', { name: 'Prévia do trecho' })).findByText('conteúdo do trecho 2')).toBeInTheDocument()

    expect(screen.getByTestId('app-shell').className.split(/\s+/)).toContain('overflow-x-clip')
    expect(scrollIntoView).not.toHaveBeenCalled()
    expect(scrollTo).toHaveBeenCalled()
    for (const [options] of scrollTo.mock.calls) expect(Object.keys(options)).toEqual(['top'])
  })
})
