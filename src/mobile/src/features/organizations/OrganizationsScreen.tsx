// PENDING (checks C17, C18, C59): placeholder so that signing in has somewhere to land.
// See .specs/features/mobile-app/plan.md "Execução".
import { router } from 'expo-router'
import { StyleSheet, Text } from 'react-native'
import { SafeAreaView } from 'react-native-safe-area-context'
import { colors, fonts } from '../../shared/theme'
import { Button } from '../../shared/ui'
import { useLogout } from '../auth/api'

export function OrganizationsScreen() {
  const logout = useLogout()
  const leave = () => logout.mutate(undefined, { onSuccess: () => router.replace('/login') })

  return (
    <SafeAreaView style={styles.screen}>
      <Text role="heading" style={styles.title}>Organizações</Text>
      <Text style={styles.text}>Tela em construção. Por enquanto, use a versão web.</Text>
      <Button title="Sair" tone="quiet" onPress={leave} />
    </SafeAreaView>
  )
}

const styles = StyleSheet.create({
  screen: { backgroundColor: colors.fog, flex: 1, gap: 16, padding: 24 },
  title: { color: colors.ink, fontFamily: fonts.display, fontSize: 24 },
  text: { color: colors.muted, fontFamily: fonts.sans, fontSize: 15 },
})
