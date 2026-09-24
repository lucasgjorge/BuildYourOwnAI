import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch, postJson } from '../../shared/api/client'
import { organizationKeys } from '../organizations/api'
import type { Assistant, CreateAssistantRequest } from './types'
import type { AskResponse } from '../../shared/api/types'

const keys = {
  one: (id: string) => ['assistants', id] as const,
}

export function useAssistant(id: string) {
  return useQuery({ queryKey: keys.one(id), queryFn: () => apiFetch<Assistant>(`/api/assistants/${id}`), retry: false })
}

export function useCreateAssistant() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: CreateAssistantRequest) => postJson<Assistant>('/api/assistants', request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: organizationKeys.all }),
  })
}

export function useDeleteAssistant() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => apiFetch<void>(`/api/assistants/${id}`, { method: 'DELETE' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: organizationKeys.all }),
  })
}

/** Asks one assistant directly; an unanswered question may open a gap, so the gaps list is refreshed. */
export function useAsk() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ assistantId, question }: { assistantId: string; question: string }) =>
      postJson<AskResponse>(`/api/assistants/${assistantId}/ask`, { question }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['gaps'] }),
  })
}
