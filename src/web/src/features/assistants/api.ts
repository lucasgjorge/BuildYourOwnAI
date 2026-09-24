import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch, postJson } from '../../shared/api/client'
import type { AskResponse, AssistantSummary, CreateAssistantRequest, DocumentItem } from './types'

const keys = {
  all: ['assistants'] as const,
  one: (id: string) => ['assistants', id] as const,
  documents: (id: string) => ['assistants', id, 'documents'] as const,
}

export function useAssistants() {
  return useQuery({ queryKey: keys.all, queryFn: () => apiFetch<AssistantSummary[]>('/api/assistants') })
}

export function useAssistant(id: string) {
  return useQuery({ queryKey: keys.one(id), queryFn: () => apiFetch<AssistantSummary>(`/api/assistants/${id}`), retry: false })
}

export function useCreateAssistant() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: CreateAssistantRequest) => postJson<AssistantSummary>('/api/assistants', request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.all }),
  })
}

export function useDeleteAssistant() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => apiFetch<void>(`/api/assistants/${id}`, { method: 'DELETE' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.all }),
  })
}

export function useDocuments(assistantId: string) {
  return useQuery({
    queryKey: keys.documents(assistantId),
    queryFn: () => apiFetch<DocumentItem[]>(`/api/assistants/${assistantId}/documents`),
  })
}

export function useUploadDocument(assistantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (file: File) => {
      const form = new FormData()
      form.append('file', file)
      return apiFetch<DocumentItem>(`/api/assistants/${assistantId}/documents`, { method: 'POST', body: form })
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.all }),
  })
}

export function useDeleteDocument(assistantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (documentId: string) =>
      apiFetch<void>(`/api/assistants/${assistantId}/documents/${documentId}`, { method: 'DELETE' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.all }),
  })
}

export function useAsk(assistantId: string) {
  return useMutation({
    mutationFn: (question: string) => postJson<AskResponse>(`/api/assistants/${assistantId}/ask`, { question }),
  })
}
