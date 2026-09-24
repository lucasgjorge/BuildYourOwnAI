import { useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import { ApiError, errorTitle } from '../../shared/api/client'
import { useAsk, useAssistant, useDeleteDocument, useDocuments, useUploadDocument } from './api'

export function AssistantPage() {
  const { id = '' } = useParams()
  const assistant = useAssistant(id)

  if (assistant.isPending) return <p className="p-6">Carregando...</p>
  if (assistant.error instanceof ApiError && assistant.error.status === 404)
    return (
      <main className="p-6">
        <p>IA não encontrada.</p>
        <Link to="/assistants" className="underline">Voltar</Link>
      </main>
    )
  if (assistant.isError) return <p role="alert" className="p-6 text-red-700">{errorTitle(assistant.error)}</p>

  return (
    <main className="mx-auto flex max-w-2xl flex-col gap-8 p-6">
      <header>
        <Link to="/assistants" className="text-sm underline">← Minhas IAs</Link>
        <h1 className="mt-2 text-2xl font-semibold">{assistant.data.name}</h1>
        {assistant.data.instructions && <p className="mt-1 text-gray-600">{assistant.data.instructions}</p>}
      </header>
      <Documents assistantId={id} />
      <Ask assistantId={id} />
    </main>
  )
}

function Documents({ assistantId }: { assistantId: string }) {
  const documents = useDocuments(assistantId)
  const upload = useUploadDocument(assistantId)
  const remove = useDeleteDocument(assistantId)
  const [file, setFile] = useState<File | null>(null)

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (file) upload.mutate(file)
  }

  const confirmDelete = (documentId: string, fileName: string) => {
    if (window.confirm(`Apagar o documento "${fileName}"?`)) remove.mutate(documentId)
  }

  return (
    <section className="flex flex-col gap-3">
      <h2 className="text-lg font-semibold">Documentos</h2>
      <form onSubmit={submit} className="flex items-center gap-3">
        <label className="flex flex-col gap-1 text-sm">
          Arquivo (PDF, TXT ou MD, até 10 MB)
          <input type="file" accept=".pdf,.txt,.md" onChange={e => setFile(e.target.files?.[0] ?? null)} />
        </label>
        <button type="submit" disabled={upload.isPending || !file} className="rounded bg-black px-4 py-2 text-white disabled:opacity-50">
          {upload.isPending ? 'Processando...' : 'Enviar'}
        </button>
      </form>
      {upload.isError && <p role="alert" className="text-red-700">{errorTitle(upload.error)}</p>}
      {remove.isError && <p role="alert" className="text-red-700">{errorTitle(remove.error)}</p>}
      {documents.isPending && <p>Carregando...</p>}
      {documents.isError && <p role="alert" className="text-red-700">{errorTitle(documents.error)}</p>}
      <ul className="flex flex-col gap-2">
        {documents.data?.map(d => (
          <li key={d.id} className="flex items-center justify-between rounded border p-2 text-sm">
            <span>{d.fileName}</span>
            <span className="flex items-center gap-4 text-gray-600">
              {d.chunkCount} trecho(s)
              <button onClick={() => confirmDelete(d.id, d.fileName)} aria-label={`Apagar documento ${d.fileName}`} className="text-red-700">
                Apagar
              </button>
            </span>
          </li>
        ))}
      </ul>
    </section>
  )
}

function Ask({ assistantId }: { assistantId: string }) {
  const ask = useAsk(assistantId)
  const [question, setQuestion] = useState('')

  const submit = (event: FormEvent) => {
    event.preventDefault()
    ask.mutate(question)
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
      {ask.data && (
        <article className="rounded border p-4">
          <p className="whitespace-pre-wrap">{ask.data.answer}</p>
          {ask.data.sources.length > 0 && (
            <>
              <h3 className="mt-4 text-sm font-semibold">Fontes</h3>
              <ul className="text-sm text-gray-600">
                {ask.data.sources.map(s => (
                  <li key={`${s.documentId}-${s.chunkIndex}`}>{s.fileName}</li>
                ))}
              </ul>
            </>
          )}
        </article>
      )}
    </section>
  )
}
