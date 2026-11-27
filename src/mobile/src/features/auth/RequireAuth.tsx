// Mirrors src/web/src/features/auth/RequireAuth.tsx: the single guard of every authenticated route.
import { Redirect, Stack } from 'expo-router'
import { useSession } from '../../shared/session'

export function RequireAuth() {
  const session = useSession()

  if (session.isPending) return null
  if (!session.data) return <Redirect href="/login" />

  return <Stack screenOptions={{ headerShown: false }} />
}
