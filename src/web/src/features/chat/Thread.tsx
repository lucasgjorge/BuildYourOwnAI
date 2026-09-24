import { useEffect, useState, useRef, type FormEvent, type KeyboardEvent, type ReactNode } from 'react'
import { Link } from 'react-router'
import { ApiError, errorTitle } from '../../shared/api/client'
import type { AskResponse } from '../../shared/api/types'
import { laneColor, laneForName } from '../../shared/lanes'
import { Button } from '../../shared/ui'
import { useAsk } from '../assistants/api'
import { useRouting } from '../routing/api'
import type { AssistantRef } from '../routing/types'

/** An AI the thread can talk to, with its lane color. */
type Target = { id: string; name: string; organizationName: string; color: string; routing?: string | null }

type Entry =
  | { key: number; kind: 'question'; text: string; to: string }
  | { key: number; kind: 'answer'; question: string; by: Target; routed: boolean; answer: AskResponse; others: Target[] }
  | { key: number; kind: 'clarify'; question: string; candidates: Target[] }
  | { key: number; kind: 'noMatch' }
  | { key: number; kind: 'noEligible' }
  | { key: number; kind: 'error'; title: string }

/** An entry before it gets its key; distributes over the union so each kind keeps its own fields. */
type WithoutKey<T> = T extends unknown ? Omit<T, 'key'> : never
type NewEntry = WithoutKey<Entry>

type Props =
  | {
      scope: 'organization'
      organizationId: string
      assistants: { id: string; name: string; routingDescription: string | null }[]
      organizationName: string
      pinnedId?: string
      /** Opens a cited chunk in the preview panel. */
      onOpenSource: (source: SourceRef) => void
      activeSource: SourceRef | null
    }
  | { scope: 'global' }

export type SourceRef = { documentId: string; chunkIndex: number }

const AUTO = 'auto'

/**
 * The conversation: questions and the answers each AI gave, kept only while the page is open (door 2).
 * With the automatic choice, a question goes to the right AI: in an organization, among its AIs; globally, among all of them.
 */
export function Thread(props: Props) {
  const organizationId = props.scope === 'organization' ? props.organizationId : null
  const routing = useRouting(organizationId)
  const ask = useAsk()
  const [entries, setEntries] = useState<Entry[]>([])
  const [message, setMessage] = useState('')
  const [target, setTarget] = useState(props.scope === 'organization' && props.pinnedId ? props.pinnedId : AUTO)
  const [pending, setPending] = useState<string | null>(null)
  const nextKey = useRef(0)

  const known: Target[] = props.scope === 'organization'
    ? props.assistants.map((a, i) => ({ id: a.id, name: a.name, organizationName: props.organizationName, color: laneColor(i), routing: a.routingDescription }))
    : []
  const toTarget = (ref: AssistantRef): Target =>
    known.find(k => k.id === ref.id) ?? { ...ref, color: laneForName(ref.name) }

  // Vertical only: scrollIntoView would also scroll sideways and cut the sidebar (source-preview, AC 17).
  useEffect(() => { window.scrollTo?.({ top: document.documentElement.scrollHeight }) }, [entries, pending])

  const push = (...added: NewEntry[]) =>
    setEntries(current => [...current, ...added.map(e => ({ ...e, key: nextKey.current++ }) as Entry)])

  // An answer found in the documents opens its first chunk in the preview; one not found cites nothing.
  const showAnswer = (entry: Omit<Extract<Entry, { kind: 'answer' }>, 'key'>) => {
    push(entry)
    const first = entry.answer.sources[0]
    if (props.scope === 'organization' && entry.answer.found && first)
      props.onOpenSource({ documentId: first.documentId, chunkIndex: first.chunkIndex })
  }

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
        showAnswer({ kind: 'answer', question, by: to, routed: false, answer, others })
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

    push({ kind: 'question', text: question, to: 'automático' })
    setPending('Escolhendo quem responde…')
    routing.mutate(question, {
      onSuccess: response => {
        if (response.kind === 'answered')
          showAnswer({
            kind: 'answer', question, by: toTarget(response.assistant), routed: true,
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
            <li className="text-muted">Pergunte qualquer coisa. A pergunta vai para a IA mais indicada entre as de todas as suas organizações.</li>
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
                <AnswerCard
                  entry={entry}
                  global={global}
                  disabled={!!pending}
                  onAsk={to => askDirect(entry.question, to, [entry.by, ...entry.others.filter(o => o.id !== to.id)])}
                  onOpenSource={props.scope === 'organization' ? props.onOpenSource : undefined}
                  activeSource={props.scope === 'organization' ? props.activeSource : null}
                />
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
                  <Link to="/gaps" className="font-medium text-route underline">Ver Lacunas</Link>
                </Note>
              )}
              {entry.kind === 'noEligible' && (
                <p role="alert" className="rounded-lg border border-danger/20 bg-danger-soft px-4 py-3 text-sm text-danger">
                  {global ? (
                    <>Preencha 'Quando usar esta IA' em pelo menos uma IA para usar a escolha automática. <Link to="/organizations" className="underline">Ir para Organizações</Link></>
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
            <li className="flex items-center gap-2 font-mono text-xs text-route" aria-live="polite">
              <span aria-hidden className="h-2 w-2 animate-pulse rounded-full bg-route" />
              {pending}
            </li>
          )}
        </ol>
      </div>

      <form onSubmit={send} className="sticky bottom-0 border-t border-line bg-surface/95 px-6 py-4 backdrop-blur md:px-10">
        <div className="mx-auto flex max-w-3xl flex-col gap-3">
          {props.scope === 'organization' && (
            <fieldset className="flex flex-wrap items-center gap-2">
              <legend className="sr-only">Para</legend>
              <span aria-hidden className="font-mono text-[11px] tracking-wider text-muted uppercase">Para</span>
              <TargetOption name="target" value={AUTO} checked={target === AUTO} onChange={setTarget} color="var(--color-route)">
                Escolha automática
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
                className="w-full resize-none rounded-lg border border-line bg-fog px-4 py-3 focus:border-route focus:bg-surface focus:outline-none"
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
      <p className="text-sm text-muted">Escreva sua pergunta. Ela vai sozinha para quem sabe responder:</p>
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

const routingOf = (t: Target) => t.routing ?? 'só responde quando escolhida no “Para” (sem “Quando usar”)'

function AnswerCard({ entry, global, disabled, onAsk, onOpenSource, activeSource }: {
  entry: Extract<Entry, { kind: 'answer' }>
  global: boolean
  disabled: boolean
  onAsk: (to: Target) => void
  onOpenSource?: (source: SourceRef) => void
  activeSource: SourceRef | null
}) {
  const { by, answer } = entry
  return (
    <article className="flex gap-3">
      <svg aria-hidden width="14" height="60" className="mt-1 shrink-0 overflow-visible">
        <circle cx="7" cy="4" r="3.5" fill={entry.routed ? 'var(--color-route)' : by.color} />
        <line x1="7" y1="8" x2="7" y2="60" stroke={by.color} strokeWidth="2" className="route-line" />
      </svg>
      <div className="min-w-0 flex-1 rounded-xl border border-line bg-surface p-4" style={{ borderLeftColor: by.color, borderLeftWidth: 3 }}>
        <header className="mb-2 flex flex-wrap items-center gap-x-3 gap-y-1">
          <span className="text-sm font-semibold" style={{ color: by.color }}>
            {global ? `Respondido por ${by.name} · ${by.organizationName}` : `Respondido por ${by.name}`}
          </span>
          {entry.routed && (
            <span className="rounded bg-route-soft px-1.5 py-0.5 font-mono text-[11px] text-route">escolha automática</span>
          )}
        </header>
        <p className="whitespace-pre-wrap">{answer.answer}</p>
        {!answer.found && (
          <p className="mt-3 text-sm text-muted">Não encontrado nos documentos - registrado em Lacunas</p>
        )}
        {/* Chunks behind an answer that was not found support nothing, so they are not cited (AC 7). */}
        {answer.found && answer.sources.length > 0 && (
          <div className="mt-4 border-t border-line pt-3">
            <h3 className="font-mono text-[11px] tracking-wider text-muted uppercase">Fontes</h3>
            <ul className="mt-2 flex flex-col gap-2">
              {groupByDocument(answer.sources).map(group => (
                <li key={group.documentId} className="flex flex-wrap items-center gap-2 text-sm">
                  <span className="font-mono text-xs text-ink/80">{group.fileName}</span>
                  {group.chunkIndexes.map(chunkIndex => {
                    const active = activeSource?.documentId === group.documentId && activeSource.chunkIndex === chunkIndex
                    return onOpenSource ? (
                      <button
                        key={chunkIndex}
                        type="button"
                        aria-pressed={active}
                        onClick={() => onOpenSource({ documentId: group.documentId, chunkIndex })}
                        className={`rounded border px-1.5 py-0.5 font-mono text-[11px] transition-colors ${
                          active ? 'border-route bg-route-soft text-route' : 'border-line text-muted hover:border-ink/40 hover:text-ink'
                        }`}
                      >
                        trecho {chunkIndex + 1}
                      </button>
                    ) : (
                      <span key={chunkIndex} className="font-mono text-[11px] text-muted">trecho {chunkIndex + 1}</span>
                    )
                  })}
                </li>
              ))}
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

/** One entry per document, in the order the answer cited them, with every cited chunk of it. */
function groupByDocument(sources: AskResponse['sources']) {
  const groups: { documentId: string; fileName: string; chunkIndexes: number[] }[] = []
  for (const source of sources) {
    const group = groups.find(g => g.documentId === source.documentId)
    if (group) group.chunkIndexes.push(source.chunkIndex)
    else groups.push({ documentId: source.documentId, fileName: source.fileName, chunkIndexes: [source.chunkIndex] })
  }
  return groups
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
      className={`inline-flex cursor-pointer items-center gap-2 rounded-full border px-3 py-1 text-sm transition-colors has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-route ${
        checked ? 'border-ink bg-ink text-surface' : 'border-line bg-surface text-ink hover:border-ink/40'
      }`}
    >
      <input type="radio" name={name} value={value} checked={checked} onChange={() => onChange(value)} className="sr-only" />
      <span aria-hidden className="h-2 w-2 rounded-full" style={{ background: color }} />
      {children}
    </label>
  )
}
