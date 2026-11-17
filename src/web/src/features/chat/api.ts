import { useQuery } from '@tanstack/react-query'
import { apiFetch } from '../../shared/api/client'

export type ConversationUsage = {
  inputTokens: number
  outputTokens: number
  totalTokens: number
}

export const conversationUsageKeys = {
  one: (conversationId: string) => ['conversation-usage', conversationId] as const,
}

export function useConversationUsage(conversationId: string, enabled: boolean) {
  return useQuery({
    queryKey: conversationUsageKeys.one(conversationId),
    queryFn: () => apiFetch<ConversationUsage>(`/api/usage/conversations/${conversationId}`),
    enabled,
    retry: false,
  })
}
