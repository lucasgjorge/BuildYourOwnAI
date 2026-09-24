import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError, apiFetch, postJson } from '../../shared/api/client'

// Mirrors src/BuildYourOwnAI.Api/Features/Auth/GetMe.cs; fullName is null for accounts created before it was asked.
export type Session = { email: string; fullName: string | null }
export type Credentials = { email: string; password: string }
export type Registration = Credentials & { fullName: string }

export const sessionKey = ['session'] as const

export function useSession() {
  return useQuery({
    queryKey: sessionKey,
    queryFn: () => apiFetch<Session>('/api/auth/me'),
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
    mutationFn: async ({ fullName, email, password }: Registration) => {
      await postJson<void>('/api/auth/register', { fullName, email, password })
      await login({ email, password })
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
