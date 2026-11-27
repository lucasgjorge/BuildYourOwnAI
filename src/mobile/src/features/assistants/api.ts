// Mirrors useAsk in src/web/src/features/assistants/api.ts.
import { useMutation } from '@tanstack/react-query'
import { postJson } from '../../shared/api/client'
import type { AskResponse } from '../../shared/api/types'

/** Asks one assistant directly. */
export function useAsk() {
  return useMutation({
    mutationFn: ({ assistantId, question, conversationId }: { assistantId: string; question: string; conversationId?: string }) =>
      postJson<AskResponse>(`/api/assistants/${assistantId}/ask`, { question, conversationId }),
  })
}
