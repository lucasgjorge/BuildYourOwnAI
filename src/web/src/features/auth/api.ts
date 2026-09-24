import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError, apiFetch, postJson } from '../../shared/api/client'

export type Session = { email: string; isEmailConfirmed: boolean }
export type Credentials = { email: string; password: string }

export const sessionKey = ['session'] as const

export function useSession() {
  return useQuery({
    queryKey: sessionKey,
    queryFn: () => apiFetch<Session>('/api/auth/manage/info'),
    retry: false,
  })
}

// Identity answers a failed login with a bare 401 titled "Unauthorized"; the user needs to know why.
async function login(credentials: Credentials) {
  try {
    await postJson<void>('/api/auth/login?useCookies=true', credentials)
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) throw new ApiError(401, 'E-mail ou senha inválidos.')
    throw error
  }
}

export function useLogin() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: login,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: sessionKey }),
  })
}

export function useRegister() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (credentials: Credentials) => {
      await postJson<void>('/api/auth/register', credentials)
      await login(credentials)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: sessionKey }),
  })
}

export function useLogout() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => apiFetch<void>('/api/auth/logout', { method: 'POST' }),
    onSuccess: () => queryClient.clear(),
  })
}
