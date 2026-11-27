// Mirrors src/web/src/features/routing/api.ts.
// PENDING (delivery 7): invalidate the gaps query once the Lacunas screen exists.
import { useMutation } from '@tanstack/react-query'
import { postJson } from '../../shared/api/client'
import type { RoutingRequest, RoutingResponse } from './types'

/** Automatic choice over one organization's assistants. */
export function useRouting(organizationId: string) {
  return useMutation({
    mutationFn: (request: RoutingRequest) =>
      postJson<RoutingResponse>(`/api/organizations/${organizationId}/route/ask`, request),
  })
}
