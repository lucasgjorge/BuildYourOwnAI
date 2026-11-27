// Mirrors the "Uso de tokens" dialog in src/web/src/features/chat/Thread.tsx.
import { Modal, Pressable, StyleSheet, Text, View } from 'react-native'
import { errorTitle } from '../../shared/api/client'
import { colors, fonts } from '../../shared/theme'
import { useConversationUsage } from './api'

const formatTokens = (tokens: number) => new Intl.NumberFormat('pt-BR').format(tokens)

export function UsageModal({ conversationId, visible, onClose }: {
  conversationId: string
  visible: boolean
  onClose: () => void
}) {
  const usage = useConversationUsage(conversationId, visible)

  return (
    <Modal transparent animationType="fade" visible={visible} onRequestClose={onClose}>
      <View style={styles.center}>
        {/* Tapping outside closes, like the web. The tint is the ink color with opacity: no literal colors. */}
        <Pressable accessibilityLabel="Fechar uso de tokens" onPress={onClose} style={[StyleSheet.absoluteFill, styles.backdrop]} />
        <View accessibilityViewIsModal style={styles.dialog}>
          <View style={styles.header}>
            <View style={styles.flex}>
              <Text role="heading" style={styles.title}>Uso de tokens</Text>
              <Text style={styles.muted}>Consumo nesta conversa</Text>
            </View>
            <Pressable role="button" accessibilityLabel="Fechar uso de tokens" onPress={onClose}>
              <Text style={styles.close}>Fechar</Text>
            </Pressable>
          </View>

          {usage.isPending ? (
            <Text style={styles.muted}>Carregando uso…</Text>
          ) : usage.isError ? (
            <Text role="alert" style={styles.error}>{errorTitle(usage.error)}</Text>
          ) : (
            <View style={styles.grid}>
              <View style={[styles.box, styles.total]}>
                <Text style={styles.muted}>Total de tokens</Text>
                <Text style={styles.totalValue}>{formatTokens(usage.data.totalTokens)}</Text>
              </View>
              <View style={[styles.box, styles.half]}>
                <Text style={styles.caption}>Entrada</Text>
                <Text style={styles.value}>{formatTokens(usage.data.inputTokens)}</Text>
              </View>
              <View style={[styles.box, styles.half]}>
                <Text style={styles.caption}>Saída</Text>
                <Text style={styles.value}>{formatTokens(usage.data.outputTokens)}</Text>
              </View>
            </View>
          )}
        </View>
      </View>
    </Modal>
  )
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  center: { alignItems: 'center', flex: 1, justifyContent: 'center', padding: 16 },
  backdrop: { backgroundColor: colors.ink, opacity: 0.5 },
  dialog: {
    backgroundColor: colors.surface,
    borderColor: colors.line,
    borderRadius: 12,
    borderWidth: 1,
    gap: 24,
    maxWidth: 448,
    padding: 24,
    width: '100%',
  },
  header: { alignItems: 'flex-start', flexDirection: 'row', gap: 16 },
  title: { color: colors.ink, fontFamily: fonts.display, fontSize: 20 },
  muted: { color: colors.muted, fontFamily: fonts.sans, fontSize: 14 },
  close: { color: colors.muted, fontFamily: fonts.sans, fontSize: 14 },
  error: { color: colors.danger, fontFamily: fonts.sans, fontSize: 14 },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: 12 },
  box: { borderRadius: 8, padding: 12 },
  total: { backgroundColor: colors.fog, padding: 16, width: '100%' },
  half: { borderColor: colors.line, borderWidth: 1, flexGrow: 1, flexBasis: '45%' },
  caption: { color: colors.muted, fontFamily: fonts.sans, fontSize: 12 },
  value: { color: colors.ink, fontFamily: fonts.mono, fontSize: 16, marginTop: 4 },
  totalValue: { color: colors.ink, fontFamily: fonts.mono, fontSize: 24, marginTop: 4 },
})
