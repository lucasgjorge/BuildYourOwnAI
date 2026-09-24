import { useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import { ApiError, errorTitle } from '../../shared/api/client'
import { AnswerView } from './AnswerView'
import { useAsk, useAssistant } from './api'

export function AssistantPage() {
  const { id = '' } = useParams()
  const assistant = useAssistant(id)

  if (assistant.isPending) return <p className="p-6">Carregando...</p>
  if (assistant.error instanceof ApiError && assistant.error.status === 404)
    return (
      <main className="p-6">
        <p>IA não encontrada.</p>
        <Link to="/organizations" className="underline">Voltar</Link>
      </main>
    )
  if (assistant.isError) return <p role="alert" className="p-6 text-red-700">{errorTitle(assistant.error)}</p>

  return (
    <main className="mx-auto flex max-w-2xl flex-col gap-8 p-6">
      <header>
        <Link to={`/organizations/${assistant.data.organizationId}`} className="text-sm underline">
          ← {assistant.data.organizationName}
        </Link>
        <h1 className="mt-2 text-2xl font-semibold">{assistant.data.name}</h1>
        {assistant.data.instructions && <p className="mt-1 text-gray-600">{assistant.data.instructions}</p>}
      </header>
      <Ask assistantId={id} />
    </main>
  )
}

function Ask({ assistantId }: { assistantId: string }) {
  const ask = useAsk()
  const [question, setQuestion] = useState('')

  const submit = (event: FormEvent) => {
    event.preventDefault()
    ask.mutate({ assistantId, question })
  }

  return (
    <section className="flex flex-col gap-3">
      <h2 className="text-lg font-semibold">Perguntar</h2>
      <form onSubmit={submit} className="flex flex-col gap-3">
        <label className="flex flex-col gap-1">
          Pergunta
          <textarea value={question} onChange={e => setQuestion(e.target.value)} rows={3} className="rounded border p-2" />
        </label>
        <button type="submit" disabled={ask.isPending} className="self-start rounded bg-black px-4 py-2 text-white disabled:opacity-50">
          {ask.isPending ? 'Processando...' : 'Perguntar'}
        </button>
      </form>
      {ask.isError && <p role="alert" className="text-red-700">{errorTitle(ask.error)}</p>}
      {ask.data && <AnswerView answer={ask.data} />}
    </section>
  )
}
