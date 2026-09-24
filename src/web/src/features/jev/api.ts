import { useMutation, useQueryClient } from '@tanstack/react-query'
import { postJson } from '../../shared/api/client'
import { gapKeys } from '../gaps/api'
import type { JevResponse } from './types'

export function useJev() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (question: string) => postJson<JevResponse>('/api/jev/ask', { question }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: gapKeys.all }),
  })
}
