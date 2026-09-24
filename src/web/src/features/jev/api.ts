import { useMutation, useQueryClient } from '@tanstack/react-query'
import { postJson } from '../../shared/api/client'
import { gapKeys } from '../gaps/api'
import type { JevResponse } from './types'

/** Jev over one organization's assistants, or over all of the user's assistants when `organizationId` is null. */
export function useJev(organizationId: string | null = null) {
  const queryClient = useQueryClient()
  const path = organizationId ? `/api/organizations/${organizationId}/jev/ask` : '/api/jev/ask'
  return useMutation({
    mutationFn: (question: string) => postJson<JevResponse>(path, { question }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: gapKeys.all }),
  })
}
