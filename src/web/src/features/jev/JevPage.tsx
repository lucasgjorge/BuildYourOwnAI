import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { ApiError, errorTitle } from '../../shared/api/client'
import { AnswerView } from '../assistants/AnswerView'
import { useAsk } from '../assistants/api'
import { useJev } from './api'
import type { AssistantRef } from './types'
import type { AskResponse } from '../../shared/api/types'

type Answered = { assistant: AssistantRef; answer: AskResponse; others: AssistantRef[] }

export function JevPage() {
  const jev = useJev()
  const ask = useAsk()
  const [question, setQuestion] = useState('')
  const [asked, setAsked] = useState('')
  const [answered, setAnswered] = useState<Answered | null>(null)
  const [candidates, setCandidates] = useState<AssistantRef[] | null>(null)
  const [noMatch, setNoMatch] = useState(false)

  const submit = (event: FormEvent) => {
    event.preventDefault()
    setAnswered(null)
    setCandidates(null)
    setNoMatch(false)
    ask.reset()
    setAsked(question)
    jev.mutate(question, {
      onSuccess: response => {
        if (response.kind === 'answered') setAnswered({ assistant: response.assistant, answer: response, others: response.alternatives })
        else if (response.kind === 'clarify') setCandidates(response.candidates)
        else setNoMatch(true)
      },
    })
  }

  // Asks the chosen assistant directly, with the question Jev received.
  const askTo = (target: AssistantRef, others: AssistantRef[]) =>
    ask.mutate({ assistantId: target.id, question: asked }, {
      onSuccess: answer => {
        setCandidates(null)
        setAnswered({ assistant: target, answer, others })
      },
    })

  const switchTo = (target: AssistantRef) => {
    if (!answered) return
    askTo(target, [answered.assistant, ...answered.others.filter(o => o.id !== target.id)])
  }

  const noEligible = jev.error instanceof ApiError && jev.error.status === 422

  return (
    <main className="mx-auto flex max-w-2xl flex-col gap-6 p-6">
      <header>
        <h1 className="text-2xl font-semibold">Jev</h1>
        <p className="mt-1 text-gray-600">Pergunte aqui e o Jev escolhe qual das suas IAs responde.</p>
      </header>

      <form onSubmit={submit} className="flex flex-col gap-3">
        <label className="flex flex-col gap-1">
          Pergunta
          <textarea value={question} onChange={e => setQuestion(e.target.value)} rows={3} className="rounded border p-2" />
        </label>
        <button type="submit" disabled={jev.isPending} className="self-start rounded bg-black px-4 py-2 text-white disabled:opacity-50">
          {jev.isPending ? 'Jev está escolhendo…' : 'Perguntar ao Jev'}
        </button>
      </form>

      {noEligible && (
        <p role="alert" className="text-red-700">
          Preencha 'Quando usar esta IA' em pelo menos uma IA para usar o Jev.{' '}
          <Link to="/organizations" className="underline">Ir para Organizações</Link>
        </p>
      )}
      {jev.isError && !noEligible && <p role="alert" className="text-red-700">{errorTitle(jev.error)}</p>}
      {ask.isError && <p role="alert" className="text-red-700">{errorTitle(ask.error)}</p>}
      {ask.isPending && <p>Processando...</p>}

      {noMatch && <p>Nenhuma IA sabe responder isso ainda. A pergunta foi para Lacunas.</p>}

      {candidates && (
        <section className="flex flex-col gap-2">
          <h2 className="font-semibold">Qual destas IAs deve responder?</h2>
          <div className="flex flex-wrap gap-2">
            {candidates.map(c => (
              <button
                key={c.id}
                onClick={() => askTo(c, candidates.filter(o => o.id !== c.id))}
                disabled={ask.isPending}
                className="rounded border px-3 py-1"
              >
                {c.name} · {c.organizationName}
              </button>
            ))}
          </div>
        </section>
      )}

      {answered && (
        <AnswerView answer={answered.answer}>
          <p className="mb-2 text-sm text-gray-600">Respondido por {answered.assistant.name} · {answered.assistant.organizationName}</p>
          {answered.others.length > 0 && (
            <div className="mb-3 flex flex-wrap items-center gap-2 text-sm">
              <span className="text-gray-600">Trocar para:</span>
              {answered.others.map(o => (
                <button key={o.id} onClick={() => switchTo(o)} disabled={ask.isPending} className="rounded border px-2 py-0.5">
                  {o.name}
                </button>
              ))}
            </div>
          )}
        </AnswerView>
      )}
    </main>
  )
}
