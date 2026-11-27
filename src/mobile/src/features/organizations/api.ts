// Mirrors src/web/src/features/organizations/api.ts (only what the app reads).
import { useQuery } from '@tanstack/react-query'
import { apiFetch } from '../../shared/api/client'
import type { OrganizationDetail, OrganizationSummary } from './types'

export const organizationKeys = {
  all: ['organizations'] as const,
  one: (id: string) => ['organizations', id] as const,
}

export function useOrganizations() {
  return useQuery({ queryKey: organizationKeys.all, queryFn: () => apiFetch<OrganizationSummary[]>('/api/organizations') })
}

export function useOrganization(id: string) {
  return useQuery({
    queryKey: organizationKeys.one(id),
    queryFn: () => apiFetch<OrganizationDetail>(`/api/organizations/${id}`),
  })
}
