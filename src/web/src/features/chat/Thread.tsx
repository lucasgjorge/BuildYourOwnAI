import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent, type ReactNode } from 'react'
import { Link } from 'react-router'
import { ApiError, errorTitle } from '../../shared/api/client'
import type { AskResponse } from '../../shared/api/types'
import { laneColor, laneForName } from '../../shared/lanes'
import { Button } from '../../shared/ui'
import { useAsk } from '../assistants/api'
import { useJev } from '../jev/api'
import type { AssistantRef } from '../jev/types'

/** An AI the thread can talk to, with its lane color. */
type Target = { id: string; name: string; organizationName: string; color: string; routing?: string | null }

type Entry =
  | { key: number; kind: 'question'; text: string; to: string }
  | { key: number; kind: 'answer'; question: string; by: Target; viaJev: boolean; answer: AskResponse; others: Target[] }
  | { key: number; kind: 'clarify'; question: string; candidates: Target[] }
  | { key: number; kind: 'noMatch' }
  | { key: number; kind: 'noEligible' }
  | { key: number; kind: 'error'; title: string }

/** An entry before it gets its key; distributes over the union so each kind keeps its own fields. */
type WithoutKey<T> = T extends unknown ? Omit<T, 'key'> : never
type NewEntry = WithoutKey<Entry>

type Props =
  | { scope: 'organization'; organizationId: string; assistants: { id: string; name: string; routingDescription: string | null }[]; organizationName: string; pinnedId?: string }
  | { scope: 'global' }

const JEV = 'jev'

/**
 * The conversation: questions and the answers each AI gave, kept only while the page is open (door 2).
 * In an organization, Jev routes among that organization's AIs; globally, among all of the user's AIs.
 */
export function Thread(props: Props) {
  const organizationId = props.scope === 'organization' ? props.organizationId : null
  const jev = useJev(organizationId)
  const ask = useAsk()
  const [entries, setEntries] = useState<Entry[]>([])
  const [message, setMessage] = useState('')
  const [target, setTarget] = useState(props.scope === 'organization' && props.pinnedId ? props.pinnedId : JEV)
  const [pending, setPending] = useState<string | null>(null)
  const nextKey = useRef(0)
  const end = useRef<HTMLDivElement>(null)

  const known: Target[] = props.scope === 'organization'
    ? props.assistants.map((a, i) => ({ id: a.id, name: a.name, organizationName: props.organizationName, color: laneColor(i), routing: a.routingDescription }))
    : []
  const toTarget = (ref: AssistantRef): Target =>
    known.find(k => k.id === ref.id) ?? { ...ref, color: laneForName(ref.name) }

  useEffect(() => { end.current?.scrollIntoView?.({ block: 'end' }) }, [entries, pending])

  const push = (...added: NewEntry[]) =>
    setEntries(current => [...current, ...added.map(e => ({ ...e, key: nextKey.current++ }) as Entry)])

  const fail = (error: unknown, text: string) => {
    if (error instanceof ApiError && error.status === 422) push({ kind: 'noEligible' })
    else push({ kind: 'error', title: errorTitle(error) })
    setMessage(text)
  }

  // `replacing`: the clarify prompt the answer takes the place of, once an AI was picked from it.
  const askDirect = (question: string, to: Target, others: Target[], replacing?: number) => {
    setPending(`${to.name} está respondendo…`)
    ask.mutate({ assistantId: to.id, question }, {
      onSuccess: answer => {
        if (replacing !== undefined) setEntries(current => current.filter(e => e.key !== replacing))
        push({ kind: 'answer', question, by: to, viaJev: false, answer, others })
      },
      onError: error => fail(error, question),
      onSettled: () => setPending(null),
    })
  }

  const send = (event?: FormEvent) => {
    event?.preventDefault()
    const question = message.trim()
    if (question === '' || pending) return
    setMessage('')

    const pinned = known.find(k => k.id === target)
    if (pinned) {
      push({ kind: 'question', text: question, to: pinned.name })
      askDirect(question, pinned, known.filter(k => k.id !== pinned.id))
      return
    }

    push({ kind: 'question', text: question, to: 'Jev' })
    setPending('Jev está escolhendo…')
    jev.mutate(question, {
      onSuccess: response => {
        if (response.kind === 'answered')
          push({
            kind: 'answer', question, by: toTarget(response.assistant), viaJev: true,
            answer: response, others: response.alternatives.map(toTarget),
          })
        else if (response.kind === 'clarify') push({ kind: 'clarify', question, candidates: response.candidates.map(toTarget) })
        else push({ kind: 'noMatch' })
      },
      onError: error => fail(error, question),
      onSettled: () => setPending(null),
    })
  }

  // Enter sends, Shift+Enter breaks the line.
  const onKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key !== 'Enter' || event.shiftKey) return
    event.preventDefault()
    send()
  }

  const global = props.scope === 'global'
  const label = (t: Target) => (global ? `${t.name} · ${t.organizationName}` : t.name)

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <div className="flex-1 px-6 py-6 md:px-10">
        <ol aria-label="Conversa" className="mx-auto flex max-w-3xl flex-col gap-5">
          {entries.length === 0 && props.scope === 'organization' && <Guide targets={known} />}
          {entries.length === 0 && global && (
            <li className="text-muted">Pergunte qualquer coisa. O Jev procura entre as IAs de todas as suas organizações.</li>
          )}
          {entries.map(entry => (
            <li key={entry.key} className="entry-rise">
              {entry.kind === 'question' && (
                <div className="ml-auto flex max-w-[85%] flex-col items-end gap-1">
                  <span className="font-mono text-[11px] text-muted">você → {entry.to.toLowerCase()}</span>
                  <p className="rounded-2xl rounded-br-sm bg-ink px-4 py-2.5 whitespace-pre-wrap text-surface">{entry.text}</p>
                </div>
              )}
              {entry.kind === 'answer' && (
                <AnswerCard entry={entry} global={global} disabled={!!pending} onAsk={to =>
                  askDirect(entry.question, to, [entry.by, ...entry.others.filter(o => o.id !== to.id)])} />
              )}
              {entry.kind === 'clarify' && (
                <Note>
                  <p className="font-medium">Qual destas IAs deve responder?</p>
                  <div className="mt-3 flex flex-wrap gap-2">
                    {entry.candidates.map(c => (
                      <LaneChip key={c.id} color={c.color} disabled={!!pending}
                        onClick={() => askDirect(entry.question, c, entry.candidates.filter(o => o.id !== c.id), entry.key)}>
                        {label(c)}
                      </LaneChip>
                    ))}
                  </div>
                </Note>
              )}
              {entry.kind === 'noMatch' && (
                <Note>
                  {global ? 'Nenhuma IA sabe responder isso ainda. A pergunta foi para Lacunas.' : 'Nenhuma IA desta organização sabe responder isso ainda. A pergunta foi para Lacunas.'}{' '}
                  <Link to="/gaps" className="font-medium text-jev underline">Ver Lacunas</Link>
                </Note>
              )}
              {entry.kind === 'noEligible' && (
                <p role="alert" className="rounded-lg border border-danger/20 bg-danger-soft px-4 py-3 text-sm text-danger">
                  {global ? (
                    <>Preencha 'Quando usar esta IA' em pelo menos uma IA para usar o Jev. <Link to="/organizations" className="underline">Ir para Organizações</Link></>
                  ) : (
                    <>Nenhuma IA desta organização tem 'Quando usar' preenchido. <Link to={`/organizations/${organizationId}/knowledge`} className="underline">Abrir a Base</Link></>
                  )}
                </p>
              )}
              {entry.kind === 'error' && (
                <p role="alert" className="rounded-lg border border-danger/20 bg-danger-soft px-4 py-3 text-sm text-danger">{entry.title}</p>
              )}
            </li>
          ))}
          {pending && (
            <li className="flex items-center gap-2 font-mono text-xs text-jev" aria-live="polite">
              <span aria-hidden className="h-2 w-2 animate-pulse rounded-full bg-jev" />
              {pending}
            </li>
          )}
        </ol>
        <div ref={end} />
      </div>

      <form onSubmit={send} className="sticky bottom-0 border-t border-line bg-surface/95 px-6 py-4 backdrop-blur md:px-10">
        <div className="mx-auto flex max-w-3xl flex-col gap-3">
          {props.scope === 'organization' && (
            <fieldset className="flex flex-wrap items-center gap-2">
              <legend className="sr-only">Para</legend>
              <span aria-hidden className="font-mono text-[11px] tracking-wider text-muted uppercase">Para</span>
              <TargetOption name="target" value={JEV} checked={target === JEV} onChange={setTarget} color="var(--color-jev)">
                Jev decide
              </TargetOption>
              {known.map(k => (
                <TargetOption key={k.id} name="target" value={k.id} checked={target === k.id} onChange={setTarget} color={k.color}>
                  {k.name}
                </TargetOption>
              ))}
            </fieldset>
          )}
          <div className="flex items-end gap-3">
            <label className="flex-1">
              <span className="sr-only">Mensagem</span>
              <textarea
                value={message}
                onChange={e => setMessage(e.target.value)}
                onKeyDown={onKeyDown}
                rows={2}
                placeholder="Escreva sua pergunta…"
                className="w-full resize-none rounded-lg border border-line bg-fog px-4 py-3 focus:border-jev focus:bg-surface focus:outline-none"
              />
            </label>
            <Button type="submit" disabled={!!pending} className="h-12">Enviar</Button>
          </div>
        </div>
      </form>
    </div>
  )
}

function Guide({ targets }: { targets: Target[] }) {
  return (
    <li className="rounded-xl border border-line bg-surface p-5">
      <p className="text-sm text-muted">Escreva sua pergunta. O Jev escolhe quem responde:</p>
      <ul className="mt-3 flex flex-col gap-2">
        {targets.map(t => (
          <li key={t.id} className="flex gap-3 text-sm">
            <span aria-hidden className="mt-1.5 h-2 w-2 shrink-0 rounded-full" style={{ background: t.color }} />
            <span><span className="font-semibold">{t.name}</span> <span className="text-muted">{routingOf(t)}</span></span>
          </li>
        ))}
      </ul>
    </li>
  )
}

const routingOf = (t: Target) => t.routing ?? 'fica fora do Jev (sem “Quando usar”)'

function AnswerCard({ entry, global, disabled, onAsk }: {
  entry: Extract<Entry, { kind: 'answer' }>
  global: boolean
  disabled: boolean
  onAsk: (to: Target) => void
}) {
  const { by, answer } = entry
  return (
    <article className="flex gap-3">
      <svg aria-hidden width="14" height="60" className="mt-1 shrink-0 overflow-visible">
        <circle cx="7" cy="4" r="3.5" fill={entry.viaJev ? 'var(--color-jev)' : by.color} />
        <line x1="7" y1="8" x2="7" y2="60" stroke={by.color} strokeWidth="2" className="route-line" />
      </svg>
      <div className="min-w-0 flex-1 rounded-xl border border-line bg-surface p-4" style={{ borderLeftColor: by.color, borderLeftWidth: 3 }}>
        <header className="mb-2 flex flex-wrap items-center gap-x-3 gap-y-1">
          <span className="text-sm font-semibold" style={{ color: by.color }}>
            {global ? `Respondido por ${by.name} · ${by.organizationName}` : `Respondido por ${by.name}`}
          </span>
          {entry.viaJev && (
            <span className="rounded bg-jev-soft px-1.5 py-0.5 font-mono text-[11px] text-jev">via Jev</span>
          )}
        </header>
        <p className="whitespace-pre-wrap">{answer.answer}</p>
        {!answer.found && (
          <p className="mt-3 text-sm text-muted">Não encontrado nos documentos - registrado em Lacunas</p>
        )}
        {answer.sources.length > 0 && (
          <div className="mt-4 border-t border-line pt-3">
            <h3 className="font-mono text-[11px] tracking-wider text-muted uppercase">Fontes</h3>
            <ul className="mt-1 flex flex-wrap gap-x-4 gap-y-1 font-mono text-xs text-ink/80">
              {answer.sources.map(s => <li key={`${s.documentId}-${s.chunkIndex}`}>{s.fileName}</li>)}
            </ul>
          </div>
        )}
        {entry.others.length > 0 && (
          <div className="mt-4 flex flex-wrap gap-2">
            {entry.others.map(o => (
              <LaneChip key={o.id} color={o.color} disabled={disabled} onClick={() => onAsk(o)}>
                Perguntar a {o.name}
              </LaneChip>
            ))}
          </div>
        )}
      </div>
    </article>
  )
}

function Note({ children }: { children: ReactNode }) {
  return <div className="rounded-xl border border-dashed border-line bg-surface px-4 py-3 text-sm">{children}</div>
}

function LaneChip({ color, children, ...props }: { color: string; children: ReactNode; onClick: () => void; disabled?: boolean }) {
  return (
    <button
      type="button"
      {...props}
      className="inline-flex items-center gap-2 rounded-full border border-line bg-surface px-3 py-1 text-sm hover:border-ink/40 disabled:opacity-50"
    >
      <span aria-hidden className="h-2 w-2 rounded-full" style={{ background: color }} />
      {children}
    </button>
  )
}

function TargetOption({ name, value, checked, onChange, color, children }: {
  name: string; value: string; checked: boolean; onChange: (v: string) => void; color: string; children: ReactNode
}) {
  return (
    <label
      className={`inline-flex cursor-pointer items-center gap-2 rounded-full border px-3 py-1 text-sm transition-colors has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-jev ${
        checked ? 'border-ink bg-ink text-surface' : 'border-line bg-surface text-ink hover:border-ink/40'
      }`}
    >
      <input type="radio" name={name} value={value} checked={checked} onChange={() => onChange(value)} className="sr-only" />
      <span aria-hidden className="h-2 w-2 rounded-full" style={{ background: color }} />
      {children}
    </label>
  )
}
