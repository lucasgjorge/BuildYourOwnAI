import { screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { renderApp } from '../../test/render'
import { deferred, loggedIn, problem, server } from '../../test/server'

const direct = { id: 'a1', name: 'Direto', organizationName: 'ACME' }
const teacher = { id: 'a2', name: 'Professor', organizationName: 'ACME' }

async function askJev(user: ReturnType<typeof renderApp>['user'], question = 'O que é fotossíntese?') {
  await user.type(await screen.findByLabelText('Mensagem'), question)
  await user.click(screen.getByRole('button', { name: 'Enviar' }))
}

describe('jev', () => {
  // C30
  it('jev answered shows who answered and switches', async () => {
    const asked: unknown[] = []
    server.use(
      loggedIn(),
      http.post('*/api/jev/ask', () => HttpResponse.json({
        kind: 'answered', assistant: direct, answer: 'Resposta curta.', found: true,
        sources: [{ documentId: 'd1', fileName: 'manual.txt', chunkIndex: 0, excerpt: 'x' }], alternatives: [teacher],
      })),
      http.post('*/api/assistants/a2/ask', async ({ request }) => {
        asked.push(await request.json())
        return HttpResponse.json({ answer: 'Explicação longa.', found: true, sources: [] })
      }),
    )
    const { user } = renderApp('/jev')

    await askJev(user)

    expect(await screen.findByText('Respondido por Direto · ACME')).toBeInTheDocument()
    expect(screen.getByText('Resposta curta.')).toBeInTheDocument()
    expect(screen.getByText('manual.txt')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Perguntar a Professor' }))

    expect(await screen.findByText('Respondido por Professor · ACME')).toBeInTheDocument()
    expect(screen.getByText('Explicação longa.')).toBeInTheDocument()
    expect(asked).toEqual([{ question: 'O que é fotossíntese?' }])
    expect(screen.getByRole('button', { name: 'Perguntar a Direto' })).toBeInTheDocument()
  })

  // C31
  it('jev clarify lets the user pick', async () => {
    server.use(
      loggedIn(),
      http.post('*/api/jev/ask', () => HttpResponse.json({ kind: 'clarify', candidates: [direct, teacher] })),
      http.post('*/api/assistants/a2/ask', () => HttpResponse.json({ answer: 'Explicação longa.', found: true, sources: [] })),
    )
    const { user } = renderApp('/jev')

    await askJev(user)

    expect(await screen.findByText('Qual destas IAs deve responder?')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Direto · ACME' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Professor · ACME' }))

    expect(await screen.findByText('Respondido por Professor · ACME')).toBeInTheDocument()
    expect(screen.getByText('Explicação longa.')).toBeInTheDocument()
    expect(screen.queryByText('Qual destas IAs deve responder?')).not.toBeInTheDocument()
  })

  // C32
  it('jev no match points to gaps', async () => {
    server.use(loggedIn(), http.post('*/api/jev/ask', () => HttpResponse.json({ kind: 'noMatch' })))
    const { user } = renderApp('/jev')

    await askJev(user)

    expect(await screen.findByText('Nenhuma IA sabe responder isso ainda. A pergunta foi para Lacunas.')).toBeInTheDocument()
  })

  // C33
  it('jev without eligible assistants explains how to enable', async () => {
    server.use(loggedIn(), http.post('*/api/jev/ask', () => problem(422, 'Nenhuma IA disponível para o Jev.')))
    const { user } = renderApp('/jev')

    await askJev(user)

    expect(await screen.findByRole('alert')).toHaveTextContent(
      "Preencha 'Quando usar esta IA' em pelo menos uma IA para usar o Jev")
    expect(screen.getByRole('link', { name: 'Ir para Organizações' })).toHaveAttribute('href', '/organizations')
  })

  // C34
  it.each([
    [429, 'Muitas perguntas em pouco tempo. Aguarde um minuto.'],
    [502, 'O provedor de IA falhou. Tente novamente em instantes.'],
  ])('jev loading and error states (%i)', async (status, title) => {
    const { gate, release } = deferred()
    server.use(loggedIn(), http.post('*/api/jev/ask', async () => {
      await gate
      return problem(status, title)
    }))
    const { user } = renderApp('/jev')

    await askJev(user, 'Qual o horário?')

    expect(await screen.findByText('Jev está escolhendo…')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Enviar' })).toBeDisabled()
    release()
    expect(await screen.findByRole('alert')).toHaveTextContent(title)
    expect(screen.getByLabelText('Mensagem')).toHaveValue('Qual o horário?')
    await waitFor(() => expect(screen.getByRole('button', { name: 'Enviar' })).toBeEnabled())
  })
})
