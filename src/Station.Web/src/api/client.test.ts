import { ApiError, StationApiClient } from './client'

describe('StationApiClient', () => {
  it('adds room bearer token without exposing it in URL', async () => { const mock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({ ok: true }), { status: 200, headers: { 'Content-Type': 'application/json' } })); const client = new StationApiClient(() => 'short-room-token', 'http://station'); await client.get('/api/queue'); const [url, request] = mock.mock.calls[0]; expect(url).toBe('http://station/api/queue'); expect(new Headers(request?.headers).get('Authorization')).toBe('Bearer short-room-token'); mock.mockRestore() })
  it('maps RFC 7807 errors', async () => { const mock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({ title: 'Token expired', code: 'auth.token_expired' }), { status: 401 })); const error = await new StationApiClient(() => null).get('/api/queue').catch(value => value); expect(error).toBeInstanceOf(ApiError); expect((error as ApiError).problem.code).toBe('auth.token_expired'); mock.mockRestore() })
})
