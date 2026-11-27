// AD-016: the token pair lives only in the device's secure storage.
import { useQuery } from '@tanstack/react-query'
import * as SecureStore from 'expo-secure-store'

const ACCESS = 'byoai.accessToken'
const REFRESH = 'byoai.refreshToken'

export type Tokens = { accessToken: string; refreshToken: string }

export const sessionKey = ['session'] as const

export async function saveTokens({ accessToken, refreshToken }: Tokens) {
  await SecureStore.setItemAsync(ACCESS, accessToken)
  await SecureStore.setItemAsync(REFRESH, refreshToken)
}

export const readAccessToken = () => SecureStore.getItemAsync(ACCESS)

export async function clearTokens() {
  await SecureStore.deleteItemAsync(ACCESS)
  await SecureStore.deleteItemAsync(REFRESH)
}

/** Whether the device holds a session. The Api is the one that says if it is still good. */
export function useSession() {
  return useQuery({ queryKey: sessionKey, queryFn: async () => (await readAccessToken()) !== null })
}
