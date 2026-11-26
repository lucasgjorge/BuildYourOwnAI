import { act, fireEvent, renderRouter, screen, within } from 'expo-router/testing-library'
import { StyleSheet } from 'react-native'
import { saveTokens } from '../../shared/session'
import { colors, fonts } from '../../shared/theme'
import { fakeApi, tokens } from '../../test/api'

const summary = { id: 'o1', name: 'Nexora', createdAt: '2026-01-01', assistantCount: 2, documentCount: 3 }
const rh = { id: 'a1', name: 'RH', routingDescription: 'férias e benefícios' }
const tech = { id: 'a2', name: 'Tech', routingDescription: null }
const detail = { id: 'o1', name: 'Nexora', createdAt: '2026-01-01', documentCount: 3, assistants: [rh, tech] }
const ref = (a: { id: string; name: string }) => ({ id: a.id, name: a.name, organizationName: 'Nexora' })

const answer = { answer: 'São 30 dias.', found: true, sources: [] as object[] }
const answered = (extra: object = {}) => ({
  status: 200,
  body: { kind: 'answered', assistant: ref(rh), alternatives: [ref(tech)], ...answer, ...extra },
})

const LIST = 'GET /api/organizations'
const ORG = 'GET /api/organizations/o1'
const ROUTE = 'POST /api/organizations/o1/route/ask'
const ASK_RH = 'POST /api/assistants/a1/ask'
const ASK_TECH = 'POST /api/assistants/a2/ask'

async function open(url: string, routes: Parameters<typeof fakeApi>[0]) {
  const api = fakeApi(routes)
  await saveTokens(tokens)
  await renderRouter('./src/app', { initialUrl: url })
  return api
}

const openChat = (routes: Parameters<typeof fakeApi>[0] = {}) =>
  open('/organizations/o1', { [ORG]: { status: 200, body: detail }, ...routes })

async function ask(question: string) {
  await fireEvent.changeText(await screen.findByLabelText('Mensagem'), question)
  await fireEvent.press(screen.getByRole('button', { name: 'Enviar' }))
}

type Api = Awaited<ReturnType<typeof open>>
const bodies = (api: Api, path: string) =>
  api.mock.calls.filter(([url]) => url.endsWith(path)).map(([, init]) => JSON.parse(init!.body as string))

const disabled = (name: string) => screen.getByRole('button', { name }).props.accessibilityState?.disabled
const color = (node: { props: { style?: unknown } }) => StyleSheet.flatten(node.props.style as object) as Record<string, unknown>

// C17
it('organizations list shows counts', async () => {
  const other = { ...summary, id: 'o2', name: 'Ábaco', assistantCount: 1, documentCount: 1 }
  await open('/organizations', { [LIST]: { status: 200, body: [summary, other] } })

  const rows = await screen.findAllByRole('button', { name: /IA/ })

  expect(rows).toHaveLength(2)
  expect(within(rows[0]).getByText('Nexora')).toBeTruthy()
  expect(within(rows[0]).getByText('2 IAs · 3 documentos')).toBeTruthy()
  expect(within(rows[1]).getByText('Ábaco')).toBeTruthy()
  expect(within(rows[1]).getByText('1 IA · 1 documento')).toBeTruthy()
})

// C18
it('empty organizations points to web', async () => {
  await open('/organizations', { [LIST]: { status: 200, body: [] } })

  expect(await screen.findByText('Você ainda não tem organizações. Crie a primeira na versão web.')).toBeTruthy()
})

// C59
it('organizations loading and error', async () => {
  await open('/organizations', { [LIST]: 'never' })
  expect(await screen.findByText('Carregando...')).toBeTruthy()
  await screen.unmount()

  await open('/organizations', { [LIST]: { status: 500, body: { title: 'Falha no servidor.' } } })
  expect(await screen.findByText('Falha no servidor.')).toBeTruthy()
})

// C19
it('organization without assistants points to web', async () => {
  await openChat({ [ORG]: { status: 200, body: { ...detail, assistants: [] } } })

  expect(await screen.findByText('Esta organização ainda não tem IAs')).toBeTruthy()
  expect(screen.getByText(/na versão web/)).toBeTruthy()
  expect(screen.queryByLabelText('Mensagem')).toBeNull()
})

// C58
it('missing organization shows api title', async () => {
  await openChat({ [ORG]: { status: 404, body: { title: 'Organização não encontrada.' } } })

  expect(await screen.findByText('Organização não encontrada.')).toBeTruthy()
})

// C20
it('empty conversation lists the assistants', async () => {
  await openChat()

  expect(await screen.findByText('férias e benefícios')).toBeTruthy()
  expect(screen.getByText('só responde quando escolhida no “Para” (sem “Quando usar”)')).toBeTruthy()
})

// C21
it('automatic choice sends the previous turn', async () => {
  const api = await openChat({ [ROUTE]: answered() })

  await ask('quantos dias de férias?')
  await screen.findByText('São 30 dias.')
  await ask('e quando?')
  await screen.findByText('e quando?')

  expect(bodies(api, '/route/ask')).toEqual([
    { question: 'quantos dias de férias?' },
    { question: 'e quando?', previous: { question: 'quantos dias de férias?', assistantId: 'a1' } },
  ])
})

// C22
it('pinned assistant is asked directly', async () => {
  const api = await openChat({ [ASK_TECH]: { status: 200, body: answer } })

  await fireEvent.press(await screen.findByRole('radio', { name: 'Tech' }))
  await ask('o deploy falhou')

  expect(await screen.findByText('Respondido por Tech')).toBeTruthy()
  expect(bodies(api, '/a2/ask')).toEqual([{ question: 'o deploy falhou' }])
  expect(screen.queryByText('escolha automática')).toBeNull()
})

// C23
it('answer offers the alternatives', async () => {
  const api = await openChat({ [ROUTE]: answered(), [ASK_TECH]: { status: 200, body: { ...answer, answer: 'Outra visão.' } } })

  await ask('férias?')
  expect(await screen.findByText('Respondido por RH')).toBeTruthy()
  expect(screen.getByText('escolha automática')).toBeTruthy()
  await fireEvent.press(screen.getByRole('button', { name: 'Perguntar a Tech' }))

  expect(await screen.findByText('Outra visão.')).toBeTruthy()
  expect(bodies(api, '/a2/ask')).toEqual([{ question: 'férias?' }])
})

const sources = [
  { documentId: 'd1', fileName: 'ferias.pdf', chunkIndex: 0, excerpt: 'a' },
  { documentId: 'd2', fileName: 'guia.md', chunkIndex: 4, excerpt: 'b' },
  { documentId: 'd1', fileName: 'ferias.pdf', chunkIndex: 2, excerpt: 'c' },
]

// C24
it('not found answer cites nothing', async () => {
  await openChat({ [ROUTE]: answered({ found: false, sources }) })

  await ask('qual o sentido da vida?')

  expect(await screen.findByText('Não encontrado nos documentos - registrado em Lacunas')).toBeTruthy()
  expect(screen.queryByText(/trecho \d/)).toBeNull()
  expect(screen.queryByText('ferias.pdf')).toBeNull()
})

// C32 (grouping and labels; opening the chunk is pending)
it('sources are grouped by document', async () => {
  await openChat({ [ROUTE]: answered({ sources }) })

  await ask('férias?')

  const groups = await screen.findAllByTestId('source-group')
  expect(groups).toHaveLength(2)
  expect(within(groups[0]).getByText('ferias.pdf')).toBeTruthy()
  expect(within(groups[0]).getAllByText(/trecho/).map(n => n.props.children.join(''))).toEqual(['trecho 1', 'trecho 3'])
  expect(within(groups[1]).getByText('guia.md')).toBeTruthy()
  expect(color(within(groups[1]).getByText(/trecho/)).fontFamily).toBe(fonts.mono)
})

// C25
it('clarify is replaced by the chosen answer', async () => {
  const api = await openChat({
    [ROUTE]: { status: 200, body: { kind: 'clarify', candidates: [ref(rh), ref(tech)] } },
    [ASK_TECH]: { status: 200, body: answer },
  })

  await ask('ajuda')
  expect(await screen.findByText('Qual destas IAs deve responder?')).toBeTruthy()
  expect(screen.getByRole('button', { name: 'RH' })).toBeTruthy()
  await fireEvent.press(screen.getByRole('button', { name: 'Tech' }))

  expect(await screen.findByText('Respondido por Tech')).toBeTruthy()
  expect(screen.queryByText('Qual destas IAs deve responder?')).toBeNull()
  expect(bodies(api, '/a2/ask')).toEqual([{ question: 'ajuda' }])
})

// C26 (message; the shortcut to Lacunas is pending)
it('no match links to gaps', async () => {
  await openChat({ [ROUTE]: { status: 200, body: { kind: 'noMatch' } } })

  await ask('previsão do tempo?')

  expect(
    await screen.findByText('Nenhuma IA desta organização sabe responder isso ainda. A pergunta foi para Lacunas.'),
  ).toBeTruthy()
})

// C27
it('no eligible assistant restores the text', async () => {
  await openChat({ [ROUTE]: { status: 422, body: { title: 'Nenhuma IA disponível.' } } })

  await ask('férias?')

  expect(await screen.findByText("Nenhuma IA desta organização tem 'Quando usar' preenchido.")).toBeTruthy()
  expect(screen.getByLabelText('Mensagem').props.value).toBe('férias?')
})

// C28
it.each([
  [{ status: 404, body: { title: 'IA não encontrada.' } }, 'IA não encontrada.'],
  [{ status: 429 }, 'Muitas requisições. Aguarde um pouco.'],
  [{ status: 502, body: { title: 'O provedor de IA falhou.' } }, 'O provedor de IA falhou.'],
  ['offline' as const, 'Não foi possível falar com o servidor.'],
])('ask errors show title and restore text (%j)', async (reply, title) => {
  await openChat({ [ROUTE]: reply })

  await ask('férias?')

  expect(await screen.findByText(title)).toBeTruthy()
  expect(screen.getByLabelText('Mensagem').props.value).toBe('férias?')
})

// C29
it('pending question disables sending', async () => {
  const api = await openChat({ [ROUTE]: answered(), [ASK_TECH]: 'never' })
  await ask('férias?')
  await screen.findByText('São 30 dias.')

  await fireEvent.press(screen.getByRole('button', { name: 'Perguntar a Tech' }))
  await screen.findByText('Tech está respondendo…')
  await fireEvent.changeText(screen.getByLabelText('Mensagem'), 'outra')

  expect(disabled('Enviar')).toBe(true)
  expect(disabled('Perguntar a Tech')).toBe(true)
  await fireEvent.press(screen.getByRole('button', { name: 'Enviar' }))
  await fireEvent.press(screen.getByRole('button', { name: 'Perguntar a Tech' }))
  expect(bodies(api, '/ask')).toHaveLength(2)
})

// C30
it('blank question is not sent', async () => {
  const api = await openChat()

  await ask('')
  await ask('   \n ')

  expect(api.mock.calls.filter(([, init]) => init?.method === 'POST')).toEqual([])
})

// C31
it('reopening organization starts empty', async () => {
  await open('/organizations', {
    [LIST]: { status: 200, body: [summary] },
    [ORG]: { status: 200, body: detail },
    [ROUTE]: answered(),
  })
  await fireEvent.press(await screen.findByRole('button', { name: /Nexora/ }))
  await ask('férias?')
  await screen.findByText('São 30 dias.')

  await fireEvent.press(screen.getByRole('button', { name: 'Voltar' }))
  await act(() => jest.runOnlyPendingTimers())
  await fireEvent.press(await screen.findByRole('button', { name: /Nexora/ }))

  expect(await screen.findByText('férias e benefícios')).toBeTruthy()
  expect(screen.queryByText('São 30 dias.')).toBeNull()
})

// C47
it('assistant lane color follows its position', async () => {
  const assistants = Array.from({ length: 7 }, (_, i) => ({ id: `a${i + 1}`, name: `IA ${i + 1}`, routingDescription: 'x' }))
  await openChat({
    [ORG]: { status: 200, body: { ...detail, assistants } },
    'POST /api/assistants/a2/ask': { status: 200, body: answer },
  })
  await screen.findByLabelText('Mensagem')

  const dots = (testID: string) => screen.getAllByTestId(testID).map(n => color(n).backgroundColor)
  const lanes = [1, 2, 3, 4, 5, 6, 1].map(n => colors[`lane-${n}` as keyof typeof colors])
  expect(dots('guide-dot')).toEqual(lanes)
  expect(dots('target-dot').slice(1)).toEqual(lanes) // the first target is the automatic choice

  await fireEvent.press(screen.getByRole('radio', { name: 'IA 2' }))
  await ask('oi')

  expect(color(await screen.findByText('Respondido por IA 2')).color).toBe(colors['lane-2'])
})
