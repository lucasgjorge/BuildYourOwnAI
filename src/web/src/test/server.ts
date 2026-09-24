import { http, HttpResponse, type HttpHandler } from 'msw'
import { setupServer } from 'msw/node'

// Every authenticated page shows the open gap count in the nav; tests override this when gaps matter.
export const server = setupServer(http.get('*/api/gaps', () => HttpResponse.json([])))

export const problem = (status: number, title: string) =>
  HttpResponse.json({ status, title }, { status, headers: { 'Content-Type': 'application/problem+json' } })

export const loggedIn = (): HttpHandler =>
  http.get('*/api/auth/manage/info', () => HttpResponse.json({ email: 'ana@test.local', isEmailConfirmed: false }))

export const anonymous = (): HttpHandler => http.get('*/api/auth/manage/info', () => new HttpResponse(null, { status: 401 }))

export const organization = {
  id: 'o1',
  name: 'ACME',
  createdAt: '2026-09-23T10:00:00Z',
  documentCount: 2,
  assistants: [
    { id: 'a1', name: 'Direto', routingDescription: 'respostas curtas' },
    { id: 'a2', name: 'Professor', routingDescription: null },
  ],
}

export const organizationSummary = { id: 'o1', name: 'ACME', createdAt: '2026-09-23T10:00:00Z', assistantCount: 2, documentCount: 2 }

export const assistant = {
  id: 'a1',
  organizationId: 'o1',
  organizationName: 'ACME',
  name: 'Direto',
  instructions: 'Responda em portugues',
  routingDescription: 'respostas curtas',
  createdAt: '2026-09-23T10:00:00Z',
}

export const documents = [
  { id: 'd2', fileName: 'politicas.pdf', sizeBytes: 2048, chunkCount: 4, uploadedAt: '2026-09-23T10:05:00Z' },
  { id: 'd1', fileName: 'manual.txt', sizeBytes: 1024, chunkCount: 2, uploadedAt: '2026-09-23T10:01:00Z' },
]

/** Handlers for a logged-in user looking at organization o1 with two documents and two assistants. */
export const organizationPageHandlers = (): HttpHandler[] => [
  loggedIn(),
  http.get('*/api/organizations/o1', () => HttpResponse.json(organization)),
  http.get('*/api/organizations/o1/documents', () => HttpResponse.json(documents)),
]

/** Handlers for a logged-in user looking at assistant a1. */
export const assistantPageHandlers = (): HttpHandler[] => [
  loggedIn(),
  http.get('*/api/assistants/a1', () => HttpResponse.json(assistant)),
]

/** A response that only resolves when the returned release() is called. */
export function deferred() {
  let release!: () => void
  const gate = new Promise<void>(resolve => { release = resolve })
  return { gate, release }
}
