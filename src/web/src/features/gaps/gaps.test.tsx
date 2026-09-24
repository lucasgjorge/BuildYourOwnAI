import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { renderApp } from '../../test/render'
import { deferred, loggedIn, organizationSummary, problem, server } from '../../test/server'

const withOrganization = {
  id: 'g1', question: 'Qual o horário?', askCount: 3, firstAskedAt: '2026-09-23T10:00:00Z', lastAskedAt: '2026-09-23T12:00:00Z',
  organization: { id: 'o1', name: 'ACME' }, assistant: { id: 'a1', name: 'Direto' },
}
const withoutOrganization = {
  id: 'g2', question: 'Vai chover amanhã?', askCount: 1, firstAskedAt: '2026-09-23T11:00:00Z', lastAskedAt: '2026-09-23T11:00:00Z',
  organization: null, assistant: null,
}

/** GET /api/gaps serves `gaps`, and an answered or dismissed gap leaves it, like the real Api. */
function gapsApi(initial: object[]) {
  let gaps = [...initial]
  const answers: unknown[] = []
  const dismissed: string[] = []
  return {
    answers,
    dismissed,
    handlers: [
      http.get('*/api/gaps', () => HttpResponse.json(gaps)),
      http.get('*/api/organizations', () => HttpResponse.json([organizationSummary])),
      http.post('*/api/gaps/:id/answer', async ({ params, request }) => {
        answers.push({ id: params.id, ...(await request.json() as object) })
        gaps = gaps.filter(g => (g as { id: string }).id !== params.id)
        return HttpResponse.json({ id: params.id, status: 'answered', documentId: 'd9' })
      }),
      http.post('*/api/gaps/:id/dismiss', ({ params }) => {
        dismissed.push(String(params.id))
        gaps = gaps.filter(g => (g as { id: string }).id !== params.id)
        return new HttpResponse(null, { status: 204 })
      }),
    ],
  }
}

const item = (question: string) => screen.getByText(question).closest('li')!

describe('gaps', () => {
  // C51
  it.each([
    [[withOrganization, withoutOrganization], 'Lacunas (2)'],
    [[], 'Lacunas'],
  ])('nav shows open gap count (%#)', async (gaps, label) => {
    server.use(loggedIn(), http.get('*/api/gaps', () => HttpResponse.json(gaps)), http.get('*/api/organizations', () => HttpResponse.json([])))

    renderApp('/organizations')

    const nav = await screen.findByRole('navigation', { name: 'Principal' })
    await waitFor(() => expect(within(nav).getByRole('link', { name: label })).toHaveAttribute('href', '/gaps'))
  })

  // C52
  it('gaps empty state', async () => {
    server.use(loggedIn(), ...gapsApi([]).handlers)

    renderApp('/gaps')

    expect(await screen.findByText('Nenhuma lacuna. Suas IAs responderam tudo o que foi perguntado.')).toBeInTheDocument()
  })

  // C53
  it('gaps answer requires organization when missing', async () => {
    const api = gapsApi([withOrganization, withoutOrganization])
    server.use(loggedIn(), ...api.handlers)
    const { user } = renderApp('/gaps')

    const orphan = within(await screen.findByText('Vai chover amanhã?').then(e => e.closest('li')!))
    await user.type(orphan.getByLabelText('Resposta'), 'Não sabemos prever o tempo.')
    expect(orphan.getByRole('button', { name: 'Responder' })).toBeDisabled()
    await user.selectOptions(orphan.getByLabelText('Organização que vai guardar a resposta'), 'o1')
    await user.click(orphan.getByRole('button', { name: 'Responder' }))

    await waitFor(() => expect(screen.queryByText('Vai chover amanhã?')).not.toBeInTheDocument())
    expect(api.answers).toEqual([{ id: 'g2', answer: 'Não sabemos prever o tempo.', organizationId: 'o1' }])

    const owned = within(item('Qual o horário?'))
    expect(owned.queryByLabelText('Organização que vai guardar a resposta')).not.toBeInTheDocument()
    await user.type(owned.getByLabelText('Resposta'), 'Das 9h às 18h.')
    await user.click(owned.getByRole('button', { name: 'Responder' }))

    await waitFor(() => expect(screen.queryByText('Qual o horário?')).not.toBeInTheDocument())
    expect(api.answers[1]).toEqual({ id: 'g1', answer: 'Das 9h às 18h.', organizationId: null })
  })

  // C54
  it('gaps dismiss confirms', async () => {
    const api = gapsApi([withOrganization])
    server.use(loggedIn(), ...api.handlers)
    const confirm = vi.fn().mockReturnValueOnce(false).mockReturnValueOnce(true)
    window.confirm = confirm
    const { user } = renderApp('/gaps')
    const button = within(await screen.findByText('Qual o horário?').then(e => e.closest('li')!)).getByRole('button', { name: 'Dispensar' })

    await user.click(button)
    expect(confirm.mock.calls[0][0]).toContain('A pergunta sai da lista e não volta')
    expect(api.dismissed).toEqual([])

    await user.click(button)
    await waitFor(() => expect(api.dismissed).toEqual(['g1']))
    await waitFor(() => expect(screen.queryByText('Qual o horário?')).not.toBeInTheDocument())
  })

  // C55
  it('gaps loading and error states', async () => {
    const { gate, release } = deferred()
    server.use(
      loggedIn(),
      http.get('*/api/gaps', async () => {
        await gate
        return HttpResponse.json([withOrganization])
      }),
      http.post('*/api/gaps/g1/answer', () => problem(502, 'O provedor de IA falhou.')),
    )
    const { user } = renderApp('/gaps')

    expect(await screen.findByText('Carregando lacunas…')).toBeInTheDocument()
    release()
    const owned = within(await screen.findByText('Qual o horário?').then(e => e.closest('li')!))
    await user.type(owned.getByLabelText('Resposta'), 'Das 9h às 18h.')
    await user.click(owned.getByRole('button', { name: 'Responder' }))
    expect(await owned.findByRole('alert')).toHaveTextContent('O provedor de IA falhou.')
  })

  // C55
  it('gaps list error shows problem title', async () => {
    server.use(loggedIn(), http.get('*/api/gaps', () => problem(500, 'Falha ao listar lacunas.')))

    renderApp('/gaps')

    expect(await screen.findByRole('alert')).toHaveTextContent('Falha ao listar lacunas.')
  })
})
