import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { renderApp } from '../../test/render'
import { deferred, loggedIn, problem, server } from '../../test/server'

const nexora = {
  id: 'o1', name: 'Nexora Tech', createdAt: '2026-09-24T10:00:00Z', documentCount: 3,
  assistants: [
    { id: 'rh', name: 'RH', routingDescription: 'Quando o usuário quer entender processos de RH' },
    { id: 'culture', name: 'Culture', routingDescription: 'Dúvidas sobre a cultura da empresa' },
    { id: 'tech', name: 'Tech Team', routingDescription: 'Desenvolvimento de software' },
  ],
}
const acme = { id: 'o2', name: 'ACME', createdAt: '2026-09-23T10:00:00Z', documentCount: 0, assistants: [{ id: 'sales', name: 'Vendas', routingDescription: 'vendas' }] }
const summary = (o: typeof nexora | typeof acme) => ({ id: o.id, name: o.name, createdAt: o.createdAt, assistantCount: o.assistants.length, documentCount: o.documentCount })

const ref = (id: string, name: string) => ({ id, name, organizationName: 'Nexora Tech' })
const answered = (by: ReturnType<typeof ref>, answer: string, extra: object = {}) => ({
  kind: 'answered', assistant: by, answer, found: true,
  sources: [
    { documentId: 'd1', fileName: '01_rh.txt', chunkIndex: 0, excerpt: 'x' },
    { documentId: 'd2', fileName: '02_cultura.txt', chunkIndex: 3, excerpt: 'y' },
  ],
  alternatives: [ref('culture', 'Culture'), ref('tech', 'Tech Team')], ...extra,
})

function chatApi() {
  return [
    loggedIn(),
    http.get('*/api/organizations', () => HttpResponse.json([summary(nexora), summary(acme)])),
    http.get('*/api/organizations/o1', () => HttpResponse.json(nexora)),
    http.get('*/api/organizations/o2', () => HttpResponse.json(acme)),
    http.get('*/api/assistants/culture', () =>
      HttpResponse.json({ id: 'culture', organizationId: 'o1', organizationName: 'Nexora Tech', name: 'Culture', instructions: null, routingDescription: 'x', createdAt: '2026-09-24T10:00:00Z' })),
  ]
}

async function send(user: ReturnType<typeof renderApp>['user'], text: string) {
  await user.type(await screen.findByLabelText('Mensagem'), text)
  await user.click(screen.getByRole('button', { name: 'Enviar' }))
}

const thread = () => screen.getByRole('list', { name: 'Conversa' })

describe('entering the product', () => {
  // C9
  it('organizations opens the first organization chat', async () => {
    server.use(...chatApi())

    renderApp('/organizations')

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/organizations/o1'))
    expect(await screen.findByLabelText('Mensagem')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Conversa' })).toHaveAttribute('aria-current', 'page')
  })

  // C10
  it('no organizations shows create and lands on knowledge', async () => {
    server.use(
      loggedIn(),
      http.post('*/api/organizations', () => HttpResponse.json({ id: 'o9', name: 'Nova', createdAt: '2026-09-24T10:00:00Z' }, { status: 201 })),
      http.get('*/api/organizations/o9', () => HttpResponse.json({ id: 'o9', name: 'Nova', createdAt: '2026-09-24T10:00:00Z', documentCount: 0, assistants: [] })),
      http.get('*/api/organizations/o9/documents', () => HttpResponse.json([])),
    )
    const { user } = renderApp('/organizations')

    const main = await screen.findByRole('main')
    expect(within(main).getByRole('heading', { name: 'Crie sua primeira organização' })).toBeInTheDocument()
    await user.type(within(main).getByLabelText('Nome da organização'), 'Nova')
    await user.click(within(main).getByRole('button', { name: 'Criar organização' }))

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/organizations/o9/knowledge'))
  })

  // C11
  it('sidebar lists organizations and destinations', async () => {
    server.use(...chatApi(), http.get('*/api/gaps', () => HttpResponse.json([{ id: 'g1' }, { id: 'g2' }])))

    renderApp('/organizations/o1')

    const nav = await screen.findByRole('navigation', { name: 'Principal' })
    expect(await within(nav).findByRole('link', { name: 'Nexora Tech' })).toHaveAttribute('href', '/organizations/o1')
    expect(within(nav).getByRole('link', { name: 'ACME' })).toHaveAttribute('href', '/organizations/o2')
    expect(within(nav).getByRole('button', { name: /Nova organização/ })).toBeInTheDocument()
    expect(within(nav).getByRole('link', { name: 'Jev (todas)' })).toHaveAttribute('href', '/jev')
    await waitFor(() => expect(within(nav).getByRole('link', { name: 'Lacunas (2)' })).toHaveAttribute('href', '/gaps'))
    expect(within(nav).getByRole('button', { name: 'Sair' })).toBeInTheDocument()
  })

  // C21
  it('organization without assistants points to base', async () => {
    // MSW uses the first matching handler, so the override goes first.
    server.use(http.get('*/api/organizations/o1', () => HttpResponse.json({ ...nexora, assistants: [] })), ...chatApi())

    renderApp('/organizations/o1')

    expect(await screen.findByText('Esta organização ainda não tem IAs')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Abrir a Base' })).toHaveAttribute('href', '/organizations/o1/knowledge')
    expect(screen.queryByLabelText('Mensagem')).not.toBeInTheDocument()
  })

  // C22
  it('empty thread shows what each assistant answers', async () => {
    server.use(...chatApi())

    renderApp('/organizations/o1')

    const guide = await screen.findByText(/O Jev escolhe quem responde/)
    const list = guide.closest('li')!
    for (const a of nexora.assistants) {
      expect(within(list).getByText(a.name)).toBeInTheDocument()
      expect(within(list).getByText(a.routingDescription)).toBeInTheDocument()
    }
  })

  // C23
  it('assistant link opens the organization chat pinned', async () => {
    server.use(...chatApi())

    renderApp('/assistants/culture')

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/organizations/o1'))
    expect(await screen.findByRole('radio', { name: 'Culture' })).toBeChecked()
    expect(screen.getByRole('radio', { name: 'Jev decide' })).not.toBeChecked()
  })
})

describe('organization chat', () => {
  // C12
  it('chat sends to organization jev and shows who answered', async () => {
    const sent: unknown[] = []
    server.use(...chatApi(), http.post('*/api/organizations/o1/jev/ask', async ({ request }) => {
      sent.push(await request.json())
      return HttpResponse.json(answered(ref('rh', 'RH'), 'Pelo portal, com 30 dias de antecedência.'))
    }))
    const { user } = renderApp('/organizations/o1')

    await send(user, 'Como peço férias?')

    const answer = (await screen.findByText('Pelo portal, com 30 dias de antecedência.')).closest('article')!
    expect(sent).toEqual([{ question: 'Como peço férias?' }])
    expect(within(thread()).getByText('Como peço férias?')).toBeInTheDocument()
    expect(within(answer).getByText('Respondido por RH')).toBeInTheDocument()
    expect(within(answer).getByText('via Jev')).toBeInTheDocument()
    expect(within(answer).getByText('01_rh.txt')).toBeInTheDocument()
    expect(within(answer).getByText('02_cultura.txt')).toBeInTheDocument()
    expect(within(answer).getByRole('button', { name: 'Perguntar a Culture' })).toBeInTheDocument()
  })

  // C13
  it('asking an alternative appends to the thread', async () => {
    const asked: unknown[] = []
    server.use(
      ...chatApi(),
      http.post('*/api/organizations/o1/jev/ask', () => HttpResponse.json(answered(ref('rh', 'RH'), 'Resposta do RH.'))),
      http.post('*/api/assistants/culture/ask', async ({ request }) => {
        asked.push(await request.json())
        return HttpResponse.json({ answer: 'Resposta da Culture.', found: true, sources: [] })
      }),
    )
    const { user } = renderApp('/organizations/o1')

    await send(user, 'Como peço férias?')
    await user.click(await screen.findByRole('button', { name: 'Perguntar a Culture' }))

    expect(await screen.findByText('Resposta da Culture.')).toBeInTheDocument()
    expect(screen.getByText('Respondido por RH')).toBeInTheDocument()
    expect(screen.getByText('Respondido por Culture')).toBeInTheDocument()
    expect(asked).toEqual([{ question: 'Como peço férias?' }])
    // Appended, in the order they happened: the question, RH's answer, then Culture's.
    const entries = Array.from(thread().children).map(e => e.textContent ?? '')
    expect(entries).toHaveLength(3)
    expect(entries[0]).toContain('Como peço férias?')
    expect(entries[1]).toContain('Resposta do RH.')
    expect(entries[2]).toContain('Resposta da Culture.')
  })

  // C14
  it('pinned assistant is asked directly', async () => {
    let jevCalls = 0
    server.use(
      ...chatApi(),
      http.post('*/api/organizations/o1/jev/ask', () => { jevCalls++; return HttpResponse.json({ kind: 'noMatch' }) }),
      http.post('*/api/assistants/tech/ask', () => HttpResponse.json({ answer: 'Use o pipeline X.', found: true, sources: [] })),
    )
    const { user } = renderApp('/organizations/o1')

    await user.click(await screen.findByRole('radio', { name: 'Tech Team' }))
    await send(user, 'Como faço deploy?')

    const answer = (await screen.findByText('Use o pipeline X.')).closest('article')!
    expect(within(answer).getByText('Respondido por Tech Team')).toBeInTheDocument()
    expect(within(answer).queryByText('via Jev')).not.toBeInTheDocument()
    expect(jevCalls).toBe(0)
  })

  // C15
  it('chat clarify lets the user pick', async () => {
    server.use(
      ...chatApi(),
      http.post('*/api/organizations/o1/jev/ask', () => HttpResponse.json({ kind: 'clarify', candidates: [ref('rh', 'RH'), ref('culture', 'Culture')] })),
      http.post('*/api/assistants/culture/ask', () => HttpResponse.json({ answer: 'Somos remotos.', found: true, sources: [] })),
    )
    const { user } = renderApp('/organizations/o1')

    await send(user, 'Posso trabalhar de casa?')
    expect(await screen.findByText('Qual destas IAs deve responder?')).toBeInTheDocument()
    await user.click(within(thread()).getByRole('button', { name: 'Culture' }))

    expect(await screen.findByText('Respondido por Culture')).toBeInTheDocument()
    expect(screen.getByText('Somos remotos.')).toBeInTheDocument()
  })

  // C16
  it('chat no match points to gaps', async () => {
    server.use(...chatApi(), http.post('*/api/organizations/o1/jev/ask', () => HttpResponse.json({ kind: 'noMatch' })))
    const { user } = renderApp('/organizations/o1')

    await send(user, 'Vai chover amanhã?')

    expect(await screen.findByText(/Nenhuma IA desta organização sabe responder isso ainda\. A pergunta foi para Lacunas\./)).toBeInTheDocument()
    expect(within(thread()).getByRole('link', { name: 'Ver Lacunas' })).toHaveAttribute('href', '/gaps')
  })

  // C17
  it('not found answer is flagged', async () => {
    server.use(
      ...chatApi(),
      http.post('*/api/organizations/o1/jev/ask', () => HttpResponse.json(answered(ref('rh', 'RH'), 'Não encontrei.', { found: false }))),
      http.post('*/api/assistants/culture/ask', () => HttpResponse.json({ answer: 'Encontrei.', found: true, sources: [] })),
    )
    const { user } = renderApp('/organizations/o1')

    await send(user, 'Qual o plano de saúde?')
    const notFound = (await screen.findByText('Não encontrei.')).closest('article')!
    expect(within(notFound).getByText('Não encontrado nos documentos - registrado em Lacunas')).toBeInTheDocument()

    await user.click(within(notFound).getByRole('button', { name: 'Perguntar a Culture' }))
    const found = (await screen.findByText('Encontrei.')).closest('article')!
    expect(within(found).queryByText('Não encontrado nos documentos - registrado em Lacunas')).not.toBeInTheDocument()
  })

  // C18
  it('chat shows who is working while pending', async () => {
    const jevGate = deferred()
    const techGate = deferred()
    server.use(
      ...chatApi(),
      http.post('*/api/organizations/o1/jev/ask', async () => { await jevGate.gate; return HttpResponse.json({ kind: 'noMatch' }) }),
      http.post('*/api/assistants/tech/ask', async () => { await techGate.gate; return HttpResponse.json({ answer: 'ok', found: true, sources: [] }) }),
    )
    const { user } = renderApp('/organizations/o1')

    await send(user, 'Pergunta um')
    expect(await screen.findByText('Jev está escolhendo…')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Enviar' })).toBeDisabled()
    jevGate.release()
    await waitFor(() => expect(screen.queryByText('Jev está escolhendo…')).not.toBeInTheDocument())

    await user.click(screen.getByRole('radio', { name: 'Tech Team' }))
    await send(user, 'Pergunta dois')
    expect(await screen.findByText('Tech Team está respondendo…')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Enviar' })).toBeDisabled()
    techGate.release()
    expect(await screen.findByText('Respondido por Tech Team')).toBeInTheDocument()
  })

  // C19
  it('chat error shows title and restores the message', async () => {
    server.use(...chatApi(), http.post('*/api/organizations/o1/jev/ask', () => problem(502, 'O provedor de IA falhou. Tente novamente em instantes.')))
    const { user } = renderApp('/organizations/o1')

    await send(user, 'Como peço férias?')

    expect(await within(thread()).findByRole('alert')).toHaveTextContent('O provedor de IA falhou. Tente novamente em instantes.')
    expect(screen.getByLabelText('Mensagem')).toHaveValue('Como peço férias?')
  })

  // C20
  it('chat without eligible assistants links to base', async () => {
    server.use(...chatApi(), http.post('*/api/organizations/o1/jev/ask', () => problem(422, 'Nenhuma IA desta organização está disponível para o Jev.')))
    const { user } = renderApp('/organizations/o1')

    await send(user, 'Como peço férias?')

    const alert = await within(thread()).findByRole('alert')
    expect(alert).toHaveTextContent("Nenhuma IA desta organização tem 'Quando usar' preenchido")
    expect(within(alert).getByRole('link', { name: 'Abrir a Base' })).toHaveAttribute('href', '/organizations/o1/knowledge')
  })

  // C24
  it('switching organization starts an empty thread', async () => {
    server.use(...chatApi(), http.post('*/api/organizations/o1/jev/ask', () => HttpResponse.json(answered(ref('rh', 'RH'), 'Resposta do RH.'))))
    const { user } = renderApp('/organizations/o1')

    await send(user, 'Como peço férias?')
    expect(await screen.findByText('Resposta do RH.')).toBeInTheDocument()
    await user.click(within(screen.getByRole('navigation', { name: 'Principal' })).getByRole('link', { name: 'ACME' }))

    expect(await screen.findByRole('radio', { name: 'Vendas' })).toBeInTheDocument()
    expect(screen.queryByText('Resposta do RH.')).not.toBeInTheDocument()
    expect(screen.queryByText('Como peço férias?')).not.toBeInTheDocument()
  })
})
