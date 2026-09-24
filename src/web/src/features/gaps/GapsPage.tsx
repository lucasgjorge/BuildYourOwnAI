import { useState, type FormEvent } from 'react'
import { errorTitle } from '../../shared/api/client'
import type { Gap } from '../../shared/api/types'
import { useOrganizations } from '../organizations/api'
import { useAnswerGap, useDismissGap, useGaps } from './api'

export function GapsPage() {
  const gaps = useGaps()
  const dismiss = useDismissGap()

  const confirmDismiss = (gap: Gap) => {
    if (window.confirm('Dispensar esta lacuna? A pergunta sai da lista e não volta.')) dismiss.mutate(gap.id)
  }

  return (
    <main className="mx-auto flex max-w-2xl flex-col gap-4 p-6">
      <header>
        <h1 className="text-2xl font-semibold">Lacunas</h1>
        <p className="mt-1 text-gray-600">Perguntas que nenhuma IA soube responder. Responda uma vez e a resposta vira documento da organização.</p>
      </header>

      {gaps.isPending && <p>Carregando lacunas…</p>}
      {gaps.isError && <p role="alert" className="text-red-700">{errorTitle(gaps.error)}</p>}
      {dismiss.isError && <p role="alert" className="text-red-700">{errorTitle(dismiss.error)}</p>}
      {gaps.data?.length === 0 && <p className="text-gray-600">Nenhuma lacuna. Suas IAs responderam tudo o que foi perguntado.</p>}

      <ul className="flex flex-col gap-4">
        {gaps.data?.map(gap => (
          <li key={gap.id} className="flex flex-col gap-2 rounded border p-4">
            <p className="font-medium">{gap.question}</p>
            <p className="text-sm text-gray-600">
              Perguntada {gap.askCount} vez(es) · {gap.organization?.name ?? 'Sem organização'}
              {gap.assistant && ` · ${gap.assistant.name}`}
            </p>
            <AnswerForm gap={gap} />
            <button onClick={() => confirmDismiss(gap)} className="self-start text-sm text-red-700">
              Dispensar
            </button>
          </li>
        ))}
      </ul>
    </main>
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
      <label className="flex flex-col gap-1 text-sm">
        Resposta
        <textarea value={answer} onChange={e => setAnswer(e.target.value)} rows={2} className="rounded border p-2" />
      </label>
      {needsOrganization && <OrganizationSelect value={organizationId} onChange={setOrganizationId} />}
      {answerGap.isError && <p role="alert" className="text-red-700">{errorTitle(answerGap.error)}</p>}
      <button
        type="submit"
        disabled={answerGap.isPending || (needsOrganization && organizationId === '')}
        className="self-start rounded bg-black px-4 py-1.5 text-sm text-white disabled:opacity-50"
      >
        {answerGap.isPending ? 'Processando...' : 'Responder'}
      </button>
    </form>
  )
}

function OrganizationSelect({ value, onChange }: { value: string; onChange: (id: string) => void }) {
  const organizations = useOrganizations()
  return (
    <label className="flex flex-col gap-1 text-sm">
      Organização que vai guardar a resposta
      <select value={value} onChange={e => onChange(e.target.value)} className="rounded border p-2">
        <option value="">Escolha…</option>
        {organizations.data?.map(o => (
          <option key={o.id} value={o.id}>{o.name}</option>
        ))}
      </select>
    </label>
  )
}
