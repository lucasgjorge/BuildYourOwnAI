// Mirrors src/web/src/shared/api/client.ts. Differences: absolute base URL and bearer token instead of cookie (AD-016).
import { readAccessToken } from '../session'

/** Error carrying the RFC 9457 problem details the Api returns (AD-007). */
export class ApiError extends Error {
  readonly status: number
  readonly title: string
  readonly errors: Record<string, string[]>

  constructor(status: number, title: string, errors: Record<string, string[]> = {}) {
    super(title)
    this.status = status
    this.title = title
    this.errors = errors
  }
}

type ProblemDetails = { title?: string; errors?: Record<string, string[]> }

const fallbackTitles: Record<number, string> = {
  401: 'Sua sessão expirou. Entre novamente.',
  429: 'Muitas requisições. Aguarde um pouco.',
}

// Written out in full: Expo only inlines `process.env.EXPO_PUBLIC_*` when it is referenced literally.
export const apiUrl = () => process.env.EXPO_PUBLIC_API_URL?.replace(/\/+$/, '')

/** The only way the app talks to the Api. */
export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers: Record<string, string> = {}
  if (typeof init.body === 'string') headers['Content-Type'] = 'application/json'
  const token = await readAccessToken()
  if (token) headers.Authorization = `Bearer ${token}`

  // PENDING (checks C11-C13, C56, C57): on 401, refresh the token once and repeat the call.
  // See .specs/features/mobile-app/plan.md "Execução".
  const response = await fetch(`${apiUrl()}${path}`, { ...init, headers, credentials: 'omit' })

  if (!response.ok) {
    const problem = await readProblem(response)
    throw new ApiError(
      response.status,
      problem.title ?? fallbackTitles[response.status] ?? `Erro ${response.status}`,
      problem.errors ?? {},
    )
  }

  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

async function readProblem(response: Response): Promise<ProblemDetails> {
  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return {}
  }
}

export const postJson = <T>(path: string, body: unknown) =>
  apiFetch<T>(path, { method: 'POST', body: JSON.stringify(body) })

/** Readable message for any error thrown by a query or mutation. */
export function errorTitle(error: unknown): string {
  if (error instanceof ApiError) {
    const details = Object.values(error.errors).flat()
    return details.length > 0 ? `${error.title} ${details.join(' ')}` : error.title
  }
  return 'Não foi possível falar com o servidor.'
}
