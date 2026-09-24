import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch, postJson } from '../../shared/api/client'
import type { DocumentChunks, DocumentItem, OrganizationDetail, OrganizationSummary } from './types'

export const organizationKeys = {
  all: ['organizations'] as const,
  one: (id: string) => ['organizations', id] as const,
  documents: (id: string) => ['organizations', id, 'documents'] as const,
}

export function useOrganizations() {
  return useQuery({ queryKey: organizationKeys.all, queryFn: () => apiFetch<OrganizationSummary[]>('/api/organizations') })
}

export function useOrganization(id: string) {
  return useQuery({
    queryKey: organizationKeys.one(id),
    queryFn: () => apiFetch<OrganizationDetail>(`/api/organizations/${id}`),
    retry: false,
  })
}

export function useCreateOrganization() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (name: string) => postJson<OrganizationSummary>('/api/organizations', { name }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: organizationKeys.all }),
  })
}

export function useDeleteOrganization() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => apiFetch<void>(`/api/organizations/${id}`, { method: 'DELETE' }),
    // Deleting an organization also removes its gaps.
    onSuccess: () => queryClient.invalidateQueries(),
  })
}

export function useDocuments(organizationId: string) {
  return useQuery({
    queryKey: organizationKeys.documents(organizationId),
    queryFn: () => apiFetch<DocumentItem[]>(`/api/organizations/${organizationId}/documents`),
  })
}

export function useUploadDocument(organizationId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (file: File) => {
      const form = new FormData()
      form.append('file', file)
      return apiFetch<DocumentItem>(`/api/organizations/${organizationId}/documents`, { method: 'POST', body: form })
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: organizationKeys.all }),
  })
}

export function useDeleteDocument(organizationId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (documentId: string) =>
      apiFetch<void>(`/api/organizations/${organizationId}/documents/${documentId}`, { method: 'DELETE' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: organizationKeys.all }),
  })
}

/** A cited chunk with one neighbor on each side, for the preview panel. */
export function useDocumentChunk(organizationId: string, documentId: string, chunkIndex: number) {
  return useQuery({
    queryKey: ['organizations', organizationId, 'documents', documentId, 'chunks', chunkIndex],
    queryFn: () => apiFetch<DocumentChunks>(`/api/organizations/${organizationId}/documents/${documentId}/chunks/${chunkIndex}?around=1`),
    retry: false,
  })
}
