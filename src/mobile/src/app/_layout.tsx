import { Stack } from 'expo-router'
import { AppProviders } from '../shared/AppProviders'

export default function RootLayout() {
  return (
    <AppProviders>
      <Stack screenOptions={{ headerShown: false }} />
    </AppProviders>
  )
}
