import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch, postJson } from '../../shared/api/client'
import { organizationKeys } from '../organizations/api'
import type { Gap } from './types'

export const gapKeys = { all: ['gaps'] as const }

export function useGaps() {
  return useQuery({ queryKey: gapKeys.all, queryFn: () => apiFetch<Gap[]>('/api/gaps') })
}

export function useAnswerGap() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, answer, organizationId }: { id: string; answer: string; organizationId: string | null }) =>
      postJson<{ id: string; status: string; documentId: string }>(`/api/gaps/${id}/answer`, { answer, organizationId }),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: gapKeys.all }),
      queryClient.invalidateQueries({ queryKey: organizationKeys.all }),
    ]),
  })
}

export function useDismissGap() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => apiFetch<void>(`/api/gaps/${id}/dismiss`, { method: 'POST' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: gapKeys.all }),
  })
}
