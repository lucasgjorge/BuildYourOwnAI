// Mirrors src/web/src/features/chat/ChatPage.tsx.
import { router, useLocalSearchParams } from 'expo-router'
import { KeyboardAvoidingView, Platform, StyleSheet, Text, View } from 'react-native'
import { SafeAreaView } from 'react-native-safe-area-context'
import { errorTitle } from '../../shared/api/client'
import { colors, fonts } from '../../shared/theme'
import { Alert, Button } from '../../shared/ui'
import { useOrganization } from '../organizations/api'
import { Thread } from './Thread'

export function ChatScreen() {
  const { id } = useLocalSearchParams<{ id: string }>()
  const organization = useOrganization(id)
  const back = () => (router.canGoBack() ? router.back() : router.replace('/organizations'))

  return (
    <SafeAreaView style={styles.screen}>
      <KeyboardAvoidingView style={styles.flex} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
        <View style={styles.header}>
          <Button title="Voltar" tone="quiet" onPress={back} />
          <Text role="heading" numberOfLines={1} style={styles.title}>{organization.data?.name ?? ''}</Text>
        </View>

        {organization.isPending && <Text style={styles.status}>Carregando...</Text>}
        {organization.isError && (
          <View style={styles.status}>
            <Alert>{errorTitle(organization.error)}</Alert>
          </View>
        )}
        {organization.data?.assistants.length === 0 && (
          <View style={styles.empty}>
            <Text style={styles.emptyTitle}>Esta organização ainda não tem IAs</Text>
            <Text style={styles.emptyText}>Suba os documentos e crie as IAs na versão web. Depois é só perguntar aqui.</Text>
          </View>
        )}
        {/* Keyed by organization: another organization starts an empty thread (org-chat door 2). */}
        {organization.data && organization.data.assistants.length > 0 && (
          <Thread key={organization.data.id} organization={organization.data} />
        )}
      </KeyboardAvoidingView>
    </SafeAreaView>
  )
}

const styles = StyleSheet.create({
  screen: { backgroundColor: colors.fog, flex: 1 },
  flex: { flex: 1 },
  header: {
    alignItems: 'center',
    backgroundColor: colors.surface,
    borderBottomColor: colors.line,
    borderBottomWidth: 1,
    flexDirection: 'row',
    gap: 12,
    padding: 12,
  },
  title: { color: colors.ink, flex: 1, fontFamily: fonts.display, fontSize: 18 },
  status: { color: colors.muted, fontFamily: fonts.sans, padding: 16 },
  empty: { alignItems: 'center', gap: 8, padding: 32 },
  emptyTitle: { color: colors.ink, fontFamily: fonts.display, fontSize: 18, textAlign: 'center' },
  emptyText: { color: colors.muted, fontFamily: fonts.sans, fontSize: 15, textAlign: 'center' },
})
