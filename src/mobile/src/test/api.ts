/** Simulated Api: no server and no network. */
type Reply = { status: number; body?: unknown } | 'offline' | 'never'

/** Routes are keyed as "METHOD /path"; a call to any other route fails the test. */
export function fakeApi(routes: Record<string, Reply>) {
  const mock = jest.fn(async (url: string, init: RequestInit = {}) => {
    const key = `${init.method ?? 'GET'} ${url.replace('http://api.test', '')}`
    const reply = routes[key]
    if (!reply) throw new Error(`unexpected call: ${key}`)
    if (reply === 'offline') throw new TypeError('Network request failed')
    if (reply === 'never') return new Promise<never>(() => {})
    const text = reply.body === undefined ? '' : JSON.stringify(reply.body)
    return {
      ok: reply.status < 300,
      status: reply.status,
      text: async () => text,
      json: async () => JSON.parse(text),
    }
  })
  globalThis.fetch = mock as unknown as typeof fetch
  return mock
}

export const tokens = { tokenType: 'Bearer', accessToken: 'access-123', expiresIn: 3600, refreshToken: 'refresh-456' }
