import { useMutation, useQueryClient } from '@tanstack/react-query'
import { postJson } from '../../shared/api/client'
import { gapKeys } from '../gaps/api'
import type { RoutingResponse } from './types'

/** Automatic choice over one organization's assistants, or over all of the user's assistants when `organizationId` is null. */
export function useRouting(organizationId: string | null = null) {
  const queryClient = useQueryClient()
  const path = organizationId ? `/api/organizations/${organizationId}/route/ask` : '/api/route/ask'
  return useMutation({
    mutationFn: (question: string) => postJson<RoutingResponse>(path, { question }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: gapKeys.all }),
  })
}
