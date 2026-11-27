// Mirrors src/web/src/features/auth/api.ts.
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { ApiError, postJson } from '../../shared/api/client'
import { clearTokens, saveTokens, sessionKey } from '../../shared/session'
import type { Credentials, TokenResponse } from './types'

// Identity answers a failed login with a bare 401 titled "Unauthorized"; the user needs to know why.
async function login(credentials: Credentials) {
  try {
    // No `useCookies`: Identity answers with a bearer token pair (AD-016).
    await saveTokens(await postJson<TokenResponse>('/api/auth/login', credentials))
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) throw new ApiError(401, 'E-mail ou senha inválidos.')
    throw error
  }
}

export function useLogin() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: login,
    onSuccess: () => queryClient.setQueryData(sessionKey, true),
  })
}

export function useLogout() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: clearTokens,
    onSuccess: () => queryClient.clear(),
  })
}
