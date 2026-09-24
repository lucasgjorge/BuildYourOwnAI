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

/**
 * The only way the web talks to the Api: same-origin relative paths, session cookie, problem details on failure.
 * The URL is resolved against the page origin so the same code runs in the browser and in jsdom tests.
 */
export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  if (typeof init.body === 'string') headers.set('Content-Type', 'application/json')

  const response = await fetch(new URL(path, window.location.origin), {
    ...init,
    headers,
    credentials: 'same-origin',
  })

  if (!response.ok) {
    const problem = await readProblem(response)
    throw new ApiError(
      response.status,
      problem.title ?? fallbackTitles[response.status] ?? `Erro ${response.status}`,
      problem.errors ?? {},
    )
  }

  if (response.status === 204 || response.headers.get('Content-Length') === '0') return undefined as T
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
