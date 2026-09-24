import { http, HttpResponse, type HttpHandler } from 'msw'
import { setupServer } from 'msw/node'

export const server = setupServer()

export const problem = (status: number, title: string) =>
  HttpResponse.json({ status, title }, { status, headers: { 'Content-Type': 'application/problem+json' } })

export const loggedIn = (): HttpHandler =>
  http.get('*/api/auth/manage/info', () => HttpResponse.json({ email: 'ana@test.local', isEmailConfirmed: false }))

export const anonymous = (): HttpHandler => http.get('*/api/auth/manage/info', () => new HttpResponse(null, { status: 401 }))

export const assistant = {
  id: 'a1',
  name: 'Suporte',
  instructions: 'Responda em portugues',
  createdAt: '2026-09-23T10:00:00Z',
  documentCount: 2,
}

export const documents = [
  { id: 'd2', fileName: 'politicas.pdf', sizeBytes: 2048, chunkCount: 4, uploadedAt: '2026-09-23T10:05:00Z' },
  { id: 'd1', fileName: 'manual.txt', sizeBytes: 1024, chunkCount: 2, uploadedAt: '2026-09-23T10:01:00Z' },
]

/** Handlers for a logged-in user looking at assistant a1 with two documents. */
export const assistantPageHandlers = (): HttpHandler[] => [
  loggedIn(),
  http.get('*/api/assistants/a1', () => HttpResponse.json(assistant)),
  http.get('*/api/assistants/a1/documents', () => HttpResponse.json(documents)),
]

/** A response that only resolves when the returned release() is called. */
export function deferred() {
  let release!: () => void
  const gate = new Promise<void>(resolve => { release = resolve })
  return { gate, release }
}
