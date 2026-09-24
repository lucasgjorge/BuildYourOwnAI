import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { errorTitle } from '../../shared/api/client'
import { Alert, Button } from '../../shared/ui'
import { SourcePreview } from '../chat/SourcePreview'
import type { SourceRef } from '../chat/Thread'
import { useDocuments } from '../organizations/api'
import { useCurrentOrganization } from '../organizations/context'
import { useAnswerStudyQuestion, useCreateStudySession } from './api'
import type { StudyAnswer, StudySession } from './types'

const COUNTS = [5, 10, 20]

/** `/organizations/:id/study` (Estudar tab): multiple-choice questions from the chosen documents. */
export function StudyPage() {
  const organization = useCurrentOrganization()
  const documents = useDocuments(organization.id)
  const create = useCreateStudySession(organization.id)
  const [excluded, setExcluded] = useState<Set<string>>(new Set())
  const [count, setCount] = useState(10)
  const [session, setSession] = useState<StudySession | null>(null)
  const [preview, setPreview] = useState<SourceRef | null>(null)

  const chosen = (documents.data ?? []).filter(d => !excluded.has(d.id)).map(d => d.id)

  const toggle = (id: string) =>
    setExcluded(current => {
      const next = new Set(current)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })

  const start = (event: FormEvent) => {
    event.preventDefault()
    create.mutate({ documentIds: chosen, questionCount: count }, { onSuccess: setSession })
  }

  let content
  if (documents.isPending) content = <p className="text-muted">Carregando...</p>
  else if (documents.isError) content = <Alert>{errorTitle(documents.error)}</Alert>
  else if (documents.data.length === 0)
    content = (
      <div className="text-center">
        <p className="font-display text-xl font-bold">Suba documentos na Base para estudar</p>
        <Link to={`/organizations/${organization.id}/knowledge`} className="mt-6 inline-block rounded-md bg-ink px-4 py-2 text-sm font-medium text-surface">
          Abrir a Base
        </Link>
      </div>
    )
  else if (session)
    content = (
      <Round
        key={session.id}
        session={session}
        onPreview={setPreview}
        onRestart={() => { setSession(null); setPreview(null); create.reset() }}
      />
    )
  else
    content = (
      <form onSubmit={start} className="flex flex-col gap-6">
        <div>
          <h2 className="font-display text-xl font-bold">Estudar</h2>
          <p className="mt-1 text-sm text-muted">Escolha o material. As perguntas saem de trechos desses documentos.</p>
        </div>
        <fieldset className="flex flex-col gap-2">
          <legend className="mb-2 text-sm font-medium">Documentos</legend>
          {documents.data.map(d => (
            <label key={d.id} className="flex items-center gap-3 rounded-md border border-line bg-surface px-3 py-2 text-sm">
              <input type="checkbox" checked={!excluded.has(d.id)} onChange={() => toggle(d.id)} className="accent-route" />
              <span className="font-mono text-[13px]">{d.fileName}</span>
            </label>
          ))}
        </fieldset>
        <fieldset className="flex flex-wrap items-center gap-2">
          <legend className="mb-2 text-sm font-medium">Quantidade de perguntas</legend>
          {COUNTS.map(n => (
            <label
              key={n}
              className={`cursor-pointer rounded-full border px-4 py-1 text-sm has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-route ${
                count === n ? 'border-ink bg-ink text-surface' : 'border-line bg-surface'
              }`}
            >
              <input type="radio" name="count" value={n} checked={count === n} onChange={() => setCount(n)} className="sr-only" />
              {n}
            </label>
          ))}
        </fieldset>
        {create.isError && <Alert>{errorTitle(create.error)}</Alert>}
        <div className="flex items-center gap-4">
          <Button type="submit" disabled={create.isPending || chosen.length === 0}>Começar</Button>
          {create.isPending && <span className="font-mono text-xs text-route" aria-live="polite">Gerando perguntas…</span>}
        </div>
      </form>
    )

  return (
    <div className="flex min-w-0 flex-1">
      <main className="mx-auto w-full min-w-0 max-w-2xl flex-1 px-6 py-8 md:px-10">{content}</main>
      {preview && (
        <SourcePreview
          key={`${preview.documentId}-${preview.chunkIndex}`}
          organizationId={organization.id}
          source={preview}
          onClose={() => setPreview(null)}
        />
      )}
    </div>
  )
}

/** One study round: a question at a time, the correction, and the score at the end. */
function Round({ session, onPreview, onRestart }: {
  session: StudySession
  onPreview: (source: SourceRef | null) => void
  onRestart: () => void
}) {
  const answer = useAnswerStudyQuestion()
  const [index, setIndex] = useState(0)
  const [choice, setChoice] = useState<number | null>(null)
  const [results, setResults] = useState<StudyAnswer[]>([])
  const [finished, setFinished] = useState(false)

  const total = session.questions.length
  const question = session.questions[index]
  const result: StudyAnswer | undefined = results[index]

  if (finished)
    return (
      <div className="flex flex-col items-start gap-6">
        <p className="font-display text-3xl font-bold">Você acertou {results.filter(r => r.correct).length} de {total}</p>
        <Button onClick={onRestart}>Estudar de novo</Button>
      </div>
    )

  const submit = () => {
    if (choice === null) return
    answer.mutate({ sessionId: session.id, questionId: question.id, option: choice }, {
      onSuccess: graded => {
        setResults(current => [...current, graded])
        // The source opens by itself only when the answer was wrong (study-mode AC 23).
        if (!graded.correct) onPreview({ documentId: graded.source.documentId, chunkIndex: graded.source.chunkIndex })
      },
    })
  }

  const next = () => {
    onPreview(null)
    setChoice(null)
    answer.reset()
    if (index + 1 < total) setIndex(index + 1)
    else setFinished(true)
  }

  const stateOf = (option: number) => {
    if (!result) return choice === option ? 'selected' : 'idle'
    if (option === result.correctOption) return 'correct'
    if (option === result.chosenOption) return 'wrong'
    return 'idle'
  }
  const styles = {
    idle: 'border-line bg-surface hover:border-ink/40',
    selected: 'border-ink bg-fog',
    correct: 'border-success bg-success-soft text-ink',
    wrong: 'border-danger bg-danger-soft text-danger',
  }

  return (
    <section aria-label="Pergunta" className="flex flex-col gap-5">
      <p className="font-mono text-xs text-muted">Pergunta {index + 1} de {total}</p>
      <h2 className="text-lg font-semibold">{question.prompt}</h2>
      <ol className="flex flex-col gap-2">
        {question.options.map((option, i) => {
          const state = stateOf(i)
          return (
            <li key={i}>
              <button
                type="button"
                data-state={state}
                aria-pressed={choice === i}
                disabled={!!result || answer.isPending}
                onClick={() => setChoice(i)}
                className={`w-full rounded-lg border px-4 py-3 text-left text-sm transition-colors disabled:cursor-default ${styles[state]}`}
              >
                {option}
              </button>
            </li>
          )
        })}
      </ol>

      {answer.isError && <Alert>{errorTitle(answer.error)}</Alert>}

      {result ? (
        <div className="flex flex-col gap-3 rounded-lg border border-line bg-surface p-4">
          {result.correct ? (
            <p className="font-semibold text-success">Certo!</p>
          ) : (
            <p className="font-semibold text-danger">A resposta certa é: {question.options[result.correctOption]}</p>
          )}
          <p className="text-sm">{result.explanation}</p>
          <div className="flex flex-wrap gap-2">
            {result.correct && (
              <Button tone="quiet" onClick={() => onPreview({ documentId: result.source.documentId, chunkIndex: result.source.chunkIndex })}>
                Ver no documento
              </Button>
            )}
            <Button onClick={next}>{index + 1 < total ? 'Próxima pergunta' : 'Ver resultado'}</Button>
          </div>
        </div>
      ) : (
        <Button onClick={submit} disabled={choice === null || answer.isPending} className="self-start">
          {answer.isPending ? 'Processando...' : 'Responder'}
        </Button>
      )}
    </section>
  )
}
