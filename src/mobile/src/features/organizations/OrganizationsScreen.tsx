// Mirrors the organizations list of src/web/src/shared/AppLayout.tsx.
import { router } from 'expo-router'
import { FlatList, Pressable, StyleSheet, Text, View } from 'react-native'
import { SafeAreaView } from 'react-native-safe-area-context'
import { errorTitle } from '../../shared/api/client'
import { colors, fonts } from '../../shared/theme'
import { Alert, Button } from '../../shared/ui'
import { useLogout } from '../auth/api'
import { useOrganizations } from './api'

const count = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`

export function OrganizationsScreen() {
  const organizations = useOrganizations()
  const logout = useLogout()
  const leave = () => logout.mutate(undefined, { onSuccess: () => router.replace('/login') })

  return (
    <SafeAreaView style={styles.screen}>
      <View style={styles.header}>
        <Text role="heading" style={styles.title}>Organizações</Text>
        <Button title="Sair" tone="quiet" onPress={leave} />
      </View>

      {organizations.isPending && <Text style={styles.text}>Carregando...</Text>}
      {organizations.isError && <Alert>{errorTitle(organizations.error)}</Alert>}
      {organizations.data && (
        <FlatList
          data={organizations.data}
          keyExtractor={o => o.id}
          contentContainerStyle={styles.list}
          ListEmptyComponent={
            <Text style={styles.text}>Você ainda não tem organizações. Crie a primeira na versão web.</Text>
          }
          renderItem={({ item }) => (
            <Pressable
              role="button"
              style={styles.card}
              onPress={() => router.push({ pathname: '/organizations/[id]', params: { id: item.id } })}
            >
              <Text style={styles.name}>{item.name}</Text>
              <Text style={styles.counts}>
                {count(item.assistantCount, 'IA', 'IAs')} · {count(item.documentCount, 'documento', 'documentos')}
              </Text>
            </Pressable>
          )}
        />
      )}
    </SafeAreaView>
  )
}

const styles = StyleSheet.create({
  screen: { backgroundColor: colors.fog, flex: 1, gap: 16, padding: 16 },
  header: { alignItems: 'center', flexDirection: 'row', justifyContent: 'space-between' },
  title: { color: colors.ink, fontFamily: fonts.display, fontSize: 24 },
  text: { color: colors.muted, fontFamily: fonts.sans, fontSize: 15 },
  list: { gap: 12 },
  card: { backgroundColor: colors.surface, borderColor: colors.line, borderRadius: 12, borderWidth: 1, gap: 4, padding: 16 },
  name: { color: colors.ink, fontFamily: fonts.sansMedium, fontSize: 17 },
  counts: { color: colors.muted, fontFamily: fonts.mono, fontSize: 12 },
})
