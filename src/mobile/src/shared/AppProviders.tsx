import { BricolageGrotesque_700Bold } from '@expo-google-fonts/bricolage-grotesque'
import { InstrumentSans_400Regular, InstrumentSans_500Medium } from '@expo-google-fonts/instrument-sans'
import { JetBrainsMono_400Regular } from '@expo-google-fonts/jetbrains-mono'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useFonts } from 'expo-font'
import { useState, type ReactNode } from 'react'
import { StyleSheet, Text, View } from 'react-native'
import { SafeAreaProvider } from 'react-native-safe-area-context'
import { apiUrl } from './api/client'
import { colors, fonts } from './theme'

/** What every screen needs: the web fonts, the query cache and a configured Api address. */
export function AppProviders({ children }: { children: ReactNode }) {
  const [queryClient] = useState(() => new QueryClient({ defaultOptions: { queries: { retry: false } } }))
  const [fontsLoaded, fontError] = useFonts({
    BricolageGrotesque_700Bold,
    InstrumentSans_400Regular,
    InstrumentSans_500Medium,
    JetBrainsMono_400Regular,
  })

  // A font that fails to load must not lock the app: system fonts take over.
  if (!fontsLoaded && !fontError) return null

  if (!apiUrl())
    return (
      <View style={styles.setup}>
        <Text style={styles.setupText}>Defina EXPO_PUBLIC_API_URL em src/mobile/.env e reinicie o Expo.</Text>
      </View>
    )

  return (
    <SafeAreaProvider>
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    </SafeAreaProvider>
  )
}

const styles = StyleSheet.create({
  setup: { backgroundColor: colors.fog, flex: 1, justifyContent: 'center', padding: 24 },
  setupText: { color: colors.ink, fontFamily: fonts.sans, fontSize: 16 },
})
