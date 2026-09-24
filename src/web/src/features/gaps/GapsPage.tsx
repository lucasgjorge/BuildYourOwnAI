import { useState, type FormEvent } from 'react'
import { errorTitle } from '../../shared/api/client'
import { Alert, Button, inputClass } from '../../shared/ui'
import { useOrganizations } from '../organizations/api'
import { useAnswerGap, useDismissGap, useGaps } from './api'
import type { Gap } from './types'

export function GapsPage() {
  const gaps = useGaps()
  const dismiss = useDismissGap()

  const confirmDismiss = (gap: Gap) => {
    if (window.confirm('Dispensar esta lacuna? A pergunta sai da lista e não volta.')) dismiss.mutate(gap.id)
  }

  return (
    <div className="flex min-h-screen flex-col">
      <header className="border-b border-line bg-surface px-6 py-6 md:px-10">
        <h1 className="font-display text-2xl font-bold tracking-tight">Lacunas</h1>
        <p className="mt-1 text-sm text-muted">
          Perguntas que nenhuma IA soube responder. Responda uma vez e a resposta vira documento da organização.
        </p>
      </header>

      <main className="mx-auto flex w-full max-w-3xl flex-col gap-4 px-6 py-8 md:px-10">
        {gaps.isPending && <p className="text-muted">Carregando lacunas…</p>}
        {gaps.isError && <Alert>{errorTitle(gaps.error)}</Alert>}
        {dismiss.isError && <Alert>{errorTitle(dismiss.error)}</Alert>}
        {gaps.data?.length === 0 && <p className="text-muted">Nenhuma lacuna. Suas IAs responderam tudo o que foi perguntado.</p>}

        <ul className="flex flex-col gap-4">
          {gaps.data?.map(gap => (
            <li key={gap.id} className="flex flex-col gap-3 rounded-xl border border-line bg-surface p-5">
              <div className="flex items-start justify-between gap-4">
                <p className="font-medium">{gap.question}</p>
                <span className="shrink-0 rounded-full bg-fog px-2.5 py-0.5 font-mono text-xs text-muted">{gap.askCount}×</span>
              </div>
              <p className="font-mono text-xs text-muted">
                Perguntada {gap.askCount} vez(es) · {gap.organization?.name ?? 'Sem organização'}
                {gap.assistant && ` · ${gap.assistant.name}`}
              </p>
              <AnswerForm gap={gap} />
              <button onClick={() => confirmDismiss(gap)} className="self-start text-sm text-danger hover:underline">
                Dispensar
              </button>
            </li>
          ))}
        </ul>
      </main>
    </div>
  )
}

function AnswerForm({ gap }: { gap: Gap }) {
  const answerGap = useAnswerGap()
  const [answer, setAnswer] = useState('')
  const [organizationId, setOrganizationId] = useState('')
  const needsOrganization = gap.organization === null

  const submit = (event: FormEvent) => {
    event.preventDefault()
    answerGap.mutate({ id: gap.id, answer, organizationId: needsOrganization ? organizationId : null })
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-2">
      <label className="flex flex-col gap-1 text-sm font-medium">
        Resposta
        <textarea value={answer} onChange={e => setAnswer(e.target.value)} rows={2} className={inputClass} />
      </label>
      {needsOrganization && <OrganizationSelect value={organizationId} onChange={setOrganizationId} />}
      {answerGap.isError && <Alert>{errorTitle(answerGap.error)}</Alert>}
      <Button type="submit" disabled={answerGap.isPending || (needsOrganization && organizationId === '')} className="self-start">
        {answerGap.isPending ? 'Processando...' : 'Responder'}
      </Button>
    </form>
  )
}

function OrganizationSelect({ value, onChange }: { value: string; onChange: (id: string) => void }) {
  const organizations = useOrganizations()
  return (
    <label className="flex flex-col gap-1 text-sm font-medium">
      Organização que vai guardar a resposta
      <select value={value} onChange={e => onChange(e.target.value)} className={inputClass}>
        <option value="">Escolha…</option>
        {organizations.data?.map(o => (
          <option key={o.id} value={o.id}>{o.name}</option>
        ))}
      </select>
    </label>
  )
}
