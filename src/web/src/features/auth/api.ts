import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch, postJson } from '../../shared/api/client'

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

export function useLogin() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (credentials: Credentials) => postJson<void>('/api/auth/login?useCookies=true', credentials),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: sessionKey }),
  })
}

export function useRegister() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (credentials: Credentials) => {
      await postJson<void>('/api/auth/register', credentials)
      await postJson<void>('/api/auth/login?useCookies=true', credentials)
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
