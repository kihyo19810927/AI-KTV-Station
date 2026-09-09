export interface ApiProblem { status: number; title: string; code: string }
export class ApiError extends Error { constructor(public readonly problem: ApiProblem) { super(problem.title) } }

export class StationApiClient {
  constructor(private readonly getToken: () => string | null, private readonly baseUrl = '') {}
  get<T>(path: string, signal?: AbortSignal) { return this.request<T>(path, { signal }) }
  post<T>(path: string, body?: unknown, signal?: AbortSignal) { return this.request<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body), signal }) }
  put<T>(path: string, body: unknown, signal?: AbortSignal) { return this.request<T>(path, { method: 'PUT', body: JSON.stringify(body), signal }) }
  delete(path: string, signal?: AbortSignal) { return this.request<void>(path, { method: 'DELETE', signal }) }
  private async request<T>(path: string, init: RequestInit): Promise<T> {
    const headers = new Headers(init.headers); headers.set('Accept', 'application/json'); if (init.body) headers.set('Content-Type', 'application/json')
    const token = this.getToken(); if (token) headers.set('Authorization', `Bearer ${token}`)
    const response = await fetch(`${this.baseUrl}${path}`, { ...init, headers })
    if (!response.ok) { const body = await response.json().catch(() => ({})) as Partial<ApiProblem>; throw new ApiError({ status: response.status, title: body.title ?? '请求失败', code: body.code ?? 'api.unknown' }) }
    return response.status === 204 ? undefined as T : response.json() as Promise<T>
  }
}
