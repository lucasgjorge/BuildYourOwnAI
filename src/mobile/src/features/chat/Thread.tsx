// Mirrors src/web/src/features/chat/Thread.tsx (organization scope).
import { useRef, useState } from 'react'
import { FlatList, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native'
import { ApiError, errorTitle } from '../../shared/api/client'
import type { AskResponse } from '../../shared/api/types'
import { laneColor, laneForName } from '../../shared/lanes'
import { colors, fonts } from '../../shared/theme'
import { Alert, Button } from '../../shared/ui'
import { useAsk } from '../assistants/api'
import type { OrganizationDetail } from '../organizations/types'
import { useRouting } from '../routing/api'
import type { AssistantRef } from '../routing/types'

/** An AI the thread can talk to, with its lane color. */
type Target = { id: string; name: string; color: string; routing?: string | null }

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

const AUTO = 'auto'

/** The conversation: kept only while the screen is open (org-chat door 2). */
export function Thread({ organization }: { organization: OrganizationDetail }) {
  const routing = useRouting(organization.id)
  const ask = useAsk()
  const [entries, setEntries] = useState<Entry[]>([])
  const [message, setMessage] = useState('')
  const [target, setTarget] = useState(AUTO)
  const [pending, setPending] = useState<string | null>(null)
  const nextKey = useRef(0)
  const list = useRef<FlatList<Entry>>(null)

  const known: Target[] = organization.assistants.map((a, i) => ({
    id: a.id,
    name: a.name,
    color: laneColor(i),
    routing: a.routingDescription,
  }))
  const toTarget = (ref: AssistantRef): Target =>
    known.find(k => k.id === ref.id) ?? { id: ref.id, name: ref.name, color: laneForName(ref.name) }

  const push = (added: NewEntry) => {
    const entry = { ...added, key: nextKey.current++ } as Entry
    setEntries(current => [...current, entry])
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
        push({ kind: 'answer', question, by: to, routed: false, answer, others })
      },
      onError: error => fail(error, question),
      onSettled: () => setPending(null),
    })
  }

  const send = () => {
    const question = message.trim()
    if (question === '' || pending) return
    setMessage('')

    const pinned = known.find(k => k.id === target)
    if (pinned) {
      push({ kind: 'question', text: question, to: pinned.name })
      askDirect(question, pinned, known.filter(k => k.id !== pinned.id))
      return
    }

    // The last answer goes along as context, so a follow-up is routed too (AD-015).
    const last = entries.findLast(e => e.kind === 'answer')
    const previous = last && { question: last.question, assistantId: last.by.id }

    push({ kind: 'question', text: question, to: 'automático' })
    setPending('Escolhendo quem responde…')
    routing.mutate({ question, previous }, {
      onSuccess: response => {
        if (response.kind === 'answered')
          push({
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

  const renderEntry = (entry: Entry) => {
    switch (entry.kind) {
      case 'question':
        return (
          <View style={styles.question}>
            <Text style={styles.caption}>você → {entry.to.toLowerCase()}</Text>
            <Text style={styles.bubble}>{entry.text}</Text>
          </View>
        )
      case 'answer':
        return (
          <AnswerCard
            entry={entry}
            disabled={!!pending}
            onAsk={to => askDirect(entry.question, to, [entry.by, ...entry.others.filter(o => o.id !== to.id)])}
          />
        )
      case 'clarify':
        return (
          <View style={styles.note}>
            <Text style={styles.strong}>Qual destas IAs deve responder?</Text>
            <View style={styles.chips}>
              {entry.candidates.map(c => (
                <LaneChip
                  key={c.id}
                  label={c.name}
                  color={c.color}
                  disabled={!!pending}
                  onPress={() => askDirect(entry.question, c, entry.candidates.filter(o => o.id !== c.id), entry.key)}
                />
              ))}
            </View>
          </View>
        )
      case 'noMatch':
        // PENDING (check C26): add the shortcut to the Lacunas tab once it exists.
        return (
          <View style={styles.note}>
            <Text style={styles.text}>Nenhuma IA desta organização sabe responder isso ainda. A pergunta foi para Lacunas.</Text>
          </View>
        )
      case 'noEligible':
        return <Alert>{"Nenhuma IA desta organização tem 'Quando usar' preenchido."}</Alert>
      case 'error':
        return <Alert>{entry.title}</Alert>
    }
  }

  return (
    <View style={styles.thread}>
      <FlatList
        ref={list}
        data={entries}
        keyExtractor={entry => String(entry.key)}
        renderItem={({ item }) => renderEntry(item)}
        contentContainerStyle={styles.list}
        keyboardShouldPersistTaps="handled"
        onContentSizeChange={() => list.current?.scrollToEnd()}
        ListEmptyComponent={<Guide targets={known} />}
        ListFooterComponent={pending ? <Text style={styles.pending}>{pending}</Text> : undefined}
      />

      <View style={styles.composer}>
        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.targets} keyboardShouldPersistTaps="handled">
          <Text style={styles.caption}>PARA</Text>
          {[{ id: AUTO, name: 'Escolha automática', color: colors.route }, ...known].map(t => (
            <TargetOption key={t.id} label={t.name} color={t.color} checked={target === t.id} onPress={() => setTarget(t.id)} />
          ))}
        </ScrollView>
        <View style={styles.row}>
          <TextInput
            accessibilityLabel="Mensagem"
            value={message}
            onChangeText={setMessage}
            placeholder="Escreva sua pergunta…"
            placeholderTextColor={colors.muted}
            multiline
            style={styles.input}
          />
          <Button title="Enviar" onPress={send} disabled={!!pending} />
        </View>
      </View>
    </View>
  )
}

function Guide({ targets }: { targets: Target[] }) {
  return (
    <View style={styles.card}>
      <Text style={styles.muted}>Escreva sua pergunta. Ela vai sozinha para quem sabe responder:</Text>
      {targets.map(t => (
        <View key={t.id} style={styles.guideRow}>
          <View testID="guide-dot" style={[styles.dot, { backgroundColor: t.color }]} />
          <View style={styles.flex}>
            <Text style={styles.strong}>{t.name}</Text>
            <Text style={styles.muted}>{t.routing ?? 'só responde quando escolhida no “Para” (sem “Quando usar”)'}</Text>
          </View>
        </View>
      ))}
    </View>
  )
}

function AnswerCard({ entry, disabled, onAsk }: {
  entry: Extract<Entry, { kind: 'answer' }>
  disabled: boolean
  onAsk: (to: Target) => void
}) {
  const { by, answer } = entry
  return (
    <View style={[styles.card, styles.answer, { borderLeftColor: by.color }]}>
      <View style={styles.chips}>
        <Text style={[styles.strong, { color: by.color }]}>Respondido por {by.name}</Text>
        {entry.routed && <Text style={styles.badge}>escolha automática</Text>}
      </View>
      <Text selectable style={styles.text}>{answer.answer}</Text>
      {!answer.found && <Text style={styles.muted}>Não encontrado nos documentos - registrado em Lacunas</Text>}
      {/* Chunks behind an answer that was not found support nothing, so they are not cited. */}
      {answer.found && answer.sources.length > 0 && (
        <View style={styles.sources}>
          <Text style={styles.caption}>FONTES</Text>
          {groupByDocument(answer.sources).map(group => (
            <View key={group.documentId} testID="source-group" style={styles.chips}>
              <Text style={styles.file}>{group.fileName}</Text>
              {/* PENDING (checks C32-C35): these become buttons that open the chunk preview. */}
              {group.chunkIndexes.map(chunkIndex => (
                <Text key={chunkIndex} style={styles.caption}>trecho {chunkIndex + 1}</Text>
              ))}
            </View>
          ))}
        </View>
      )}
      {entry.others.length > 0 && (
        <View style={styles.chips}>
          {entry.others.map(o => (
            <LaneChip key={o.id} label={`Perguntar a ${o.name}`} color={o.color} disabled={disabled} onPress={() => onAsk(o)} />
          ))}
        </View>
      )}
    </View>
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

function LaneChip({ label, color, disabled, onPress }: { label: string; color: string; disabled: boolean; onPress: () => void }) {
  return (
    <Pressable
      role="button"
      accessibilityState={{ disabled }}
      disabled={disabled}
      onPress={onPress}
      style={[styles.chip, disabled && styles.disabled]}
    >
      <View style={[styles.dot, { backgroundColor: color }]} />
      <Text style={styles.chipText}>{label}</Text>
    </Pressable>
  )
}

function TargetOption({ label, color, checked, onPress }: { label: string; color: string; checked: boolean; onPress: () => void }) {
  return (
    <Pressable role="radio" accessibilityState={{ checked }} onPress={onPress} style={[styles.chip, checked && styles.checked]}>
      <View testID="target-dot" style={[styles.dot, { backgroundColor: color }]} />
      <Text style={[styles.chipText, checked && styles.checkedText]}>{label}</Text>
    </Pressable>
  )
}

const styles = StyleSheet.create({
  thread: { flex: 1 },
  flex: { flex: 1 },
  list: { gap: 16, padding: 16 },
  question: { alignItems: 'flex-end', alignSelf: 'flex-end', gap: 4, maxWidth: '85%' },
  bubble: {
    backgroundColor: colors.ink,
    borderRadius: 16,
    color: colors.surface,
    fontFamily: fonts.sans,
    fontSize: 15,
    overflow: 'hidden',
    paddingHorizontal: 14,
    paddingVertical: 10,
  },
  card: { backgroundColor: colors.surface, borderColor: colors.line, borderRadius: 12, borderWidth: 1, gap: 12, padding: 16 },
  answer: { borderLeftWidth: 3 },
  note: { backgroundColor: colors.surface, borderColor: colors.line, borderRadius: 12, borderStyle: 'dashed', borderWidth: 1, gap: 12, padding: 14 },
  text: { color: colors.ink, fontFamily: fonts.sans, fontSize: 15, lineHeight: 22 },
  strong: { color: colors.ink, fontFamily: fonts.sansMedium, fontSize: 15 },
  muted: { color: colors.muted, fontFamily: fonts.sans, fontSize: 14 },
  caption: { color: colors.muted, fontFamily: fonts.mono, fontSize: 11 },
  file: { color: colors.ink, fontFamily: fonts.mono, fontSize: 12 },
  badge: {
    backgroundColor: colors['route-soft'],
    borderRadius: 4,
    color: colors.route,
    fontFamily: fonts.mono,
    fontSize: 11,
    overflow: 'hidden',
    paddingHorizontal: 6,
    paddingVertical: 2,
  },
  sources: { borderTopColor: colors.line, borderTopWidth: 1, gap: 8, paddingTop: 12 },
  chips: { alignItems: 'center', flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  chip: {
    alignItems: 'center',
    backgroundColor: colors.surface,
    borderColor: colors.line,
    borderRadius: 999,
    borderWidth: 1,
    flexDirection: 'row',
    gap: 8,
    paddingHorizontal: 12,
    paddingVertical: 8,
  },
  chipText: { color: colors.ink, fontFamily: fonts.sans, fontSize: 14 },
  checked: { backgroundColor: colors.ink, borderColor: colors.ink },
  checkedText: { color: colors.surface },
  disabled: { opacity: 0.5 },
  dot: { borderRadius: 4, height: 8, width: 8 },
  guideRow: { alignItems: 'flex-start', flexDirection: 'row', gap: 12 },
  pending: { color: colors.route, fontFamily: fonts.mono, fontSize: 12, paddingTop: 16 },
  composer: { backgroundColor: colors.surface, borderTopColor: colors.line, borderTopWidth: 1, gap: 10, padding: 12 },
  targets: { alignItems: 'center', gap: 8 },
  row: { alignItems: 'flex-end', flexDirection: 'row', gap: 8 },
  input: {
    backgroundColor: colors.fog,
    borderColor: colors.line,
    borderRadius: 8,
    borderWidth: 1,
    color: colors.ink,
    flex: 1,
    fontFamily: fonts.sans,
    fontSize: 16,
    maxHeight: 120,
    paddingHorizontal: 12,
    paddingVertical: 10,
  },
})
