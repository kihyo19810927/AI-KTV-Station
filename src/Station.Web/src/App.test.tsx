import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, vi } from 'vitest'
import { App } from './App'
import { SessionProvider } from './state/session'

const validSession = { roomId: 'room-1', roomName: '客厅 KTV', guestId: 'guest-1', nickname: '小明', role: 'Guest' as const, token: 'short-lived-token', expiresAt: '2099-01-01T00:00:00Z' }
function renderApp(path: string) { return render(<MemoryRouter initialEntries={[path]}><SessionProvider><App /></SessionProvider></MemoryRouter>) }

describe('mobile application shell', () => {
  beforeEach(() => { sessionStorage.clear(); vi.restoreAllMocks(); vi.spyOn(globalThis, 'fetch').mockImplementation(async () => new Response(JSON.stringify({ items: [], total: 0, page: 1, pageSize: 20 }), { status: 200, headers: { 'Content-Type': 'application/json' } })) })
  it('renders join route', () => { renderApp('/join'); expect(screen.getByRole('heading', { name: 'AI-KTV Station' })).toBeInTheDocument(); expect(screen.getByRole('button', { name: '加入房间' })).toBeInTheDocument() })
  it('restores a valid session and renders the approved discovery shell', () => { sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/discover'); expect(screen.getByText('小明 · 访客')).toBeInTheDocument(); expect(screen.getByPlaceholderText('搜索歌名、歌手或拼音')).toBeInTheDocument(); expect(screen.getByRole('navigation', { name: '主导航' })).toBeInTheDocument() })
  it('joins a room and persists the short-lived session', async () => {
    vi.mocked(fetch).mockResolvedValue(new Response(JSON.stringify({ ...validSession, guestId: 'guest-1' }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
    renderApp('/join?code=ktv826')
    expect(screen.getByLabelText('房间码')).toHaveValue('KTV826')
    fireEvent.change(screen.getByLabelText('昵称'), { target: { value: '小明' } })
    fireEvent.click(screen.getByRole('button', { name: '加入房间' }))
    await screen.findByPlaceholderText('搜索歌名、歌手或拼音')
    expect(JSON.parse(sessionStorage.getItem('ai-ktv-station.room-session.v1') ?? '{}')).toMatchObject({ token: 'short-lived-token', nickname: '小明' })
    expect(fetch).toHaveBeenCalledWith('/api/rooms/join', expect.objectContaining({ method: 'POST' }))
  })
  it('clears the session when the guest exits', async () => { sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/discover'); fireEvent.click(screen.getByRole('button', { name: '退出' })); await waitFor(() => expect(sessionStorage.getItem('ai-ktv-station.room-session.v1')).toBeNull()) })
  it('debounces catalog search and sends selected filters', async () => {
    vi.mocked(fetch).mockImplementation(async () => new Response(JSON.stringify({ items: [{ songId: 'song-1', title: '夜曲', artists: '周杰伦', language: '国语', quality: '4K', availability: 'Available' }], total: 1, page: 1, pageSize: 20 }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
    sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/discover')
    fireEvent.change(screen.getByPlaceholderText('搜索歌名、歌手或拼音'), { target: { value: 'yequ' } }); fireEvent.change(screen.getByLabelText('语言'), { target: { value: '国语' } })
    await screen.findByText('夜曲')
    await waitFor(() => expect(vi.mocked(fetch).mock.calls.some(([url]) => String(url).includes('text=yequ') && String(url).includes(encodeURIComponent('国语')))).toBe(true), { timeout: 1500 })
  })
  it('shows catalog failure and retries', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify({ title: '曲库不可用', code: 'catalog.offline' }), { status: 503 })).mockResolvedValueOnce(new Response(JSON.stringify({ items: [], total: 0, page: 1, pageSize: 20 }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
    sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/discover'); expect(await screen.findByRole('alert')).toHaveTextContent('曲库不可用'); fireEvent.click(screen.getByRole('button', { name: '重试' })); expect(await screen.findByText('没有找到歌曲')).toBeInTheDocument()
  })
  it('appends the next catalog page', async () => {
    vi.mocked(fetch).mockImplementation(async url => { const page = new URL(String(url), 'http://station').searchParams.get('page'); const item = page === '2' ? { songId: 'song-2', title: '第二首', artists: '歌手乙', availability: 'Available' } : { songId: 'song-1', title: '第一首', artists: '歌手甲', availability: 'Available' }; return new Response(JSON.stringify({ items: [item], total: 2, page: Number(page), pageSize: 1 }), { status: 200, headers: { 'Content-Type': 'application/json' } }) })
    sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/discover'); await screen.findByText('第一首'); fireEvent.click(screen.getByRole('button', { name: /继续加载/ })); await screen.findByText('第二首'); expect(screen.getByText('第一首')).toBeInTheDocument()
  })
  it('requests a song and reports duplicate requests', async () => {
    const song = { songId: 'song-1', title: '夜曲', artists: '周杰伦', availability: 'Available' }
    let queue: unknown[] = []
    vi.mocked(fetch).mockImplementation(async (url, init) => { const path = String(url); if (path.startsWith('/api/catalog')) return new Response(JSON.stringify({ items: [song], total: 1, page: 1, pageSize: 20 }), { status: 200, headers: { 'Content-Type': 'application/json' } }); if (path === '/api/queue' && init?.method === 'POST') { const item = { id: 'item-1', ...song, requestedByGuestId: 'guest-1', requestedByNickname: '小明', status: 'Waiting' }; queue = [item]; return new Response(JSON.stringify(item), { status: 201, headers: { 'Content-Type': 'application/json' } }) } return new Response(JSON.stringify(queue), { status: 200, headers: { 'Content-Type': 'application/json' } }) })
    sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/discover'); await screen.findByText('夜曲'); fireEvent.click(screen.getByRole('button', { name: '点播 夜曲' })); expect(await screen.findByRole('status')).toHaveTextContent('已点播《夜曲》'); fireEvent.click(screen.getByRole('button', { name: '点播 夜曲' })); expect(await screen.findByRole('status')).toHaveTextContent('已经在你的队列中')
    expect(vi.mocked(fetch).mock.calls.filter(([, init]) => init?.method === 'POST')).toHaveLength(1)
  })
  it('lists only my songs and removes a waiting item', async () => {
    const items = [{ id: 'mine-1', songId: 'song-1', title: '我的歌', requestedByGuestId: 'guest-1', requestedByNickname: '小明', position: 1, status: 'Waiting', requestedAt: '2026-09-10T00:00:00Z' }, { id: 'other-1', songId: 'song-2', title: '别人的歌', requestedByGuestId: 'guest-2', requestedByNickname: '朋友', position: 2, status: 'Waiting', requestedAt: '2026-09-10T00:00:00Z' }]
    vi.mocked(fetch).mockImplementation(async (_url, init) => init?.method === 'DELETE' ? new Response(null, { status: 204 }) : new Response(JSON.stringify(items), { status: 200, headers: { 'Content-Type': 'application/json' } }))
    sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/queue'); await screen.findByText('我的歌'); expect(screen.queryByText('别人的歌')).not.toBeInTheDocument(); expect(screen.getByText('我已点 1 首 · 房间共 2 首')).toBeInTheDocument(); fireEvent.click(screen.getByRole('button', { name: '删除 我的歌' })); await waitFor(() => expect(screen.queryByText('我的歌')).not.toBeInTheDocument())
  })
  it('translates the server-side guest queue limit', async () => {
    const song = { songId: 'song-1', title: '夜曲', artists: '周杰伦', availability: 'Available' }
    vi.mocked(fetch).mockImplementation(async (url, init) => { if (String(url).startsWith('/api/catalog')) return new Response(JSON.stringify({ items: [song], total: 1, page: 1, pageSize: 20 }), { status: 200, headers: { 'Content-Type': 'application/json' } }); if (init?.method === 'POST') return new Response(JSON.stringify({ title: 'Guest queue limit was reached.', code: 'queue.guest_limit_reached' }), { status: 409 }); return new Response(JSON.stringify([]), { status: 200, headers: { 'Content-Type': 'application/json' } }) })
    sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/discover'); await screen.findByText('夜曲'); fireEvent.click(screen.getByRole('button', { name: '点播 夜曲' })); expect(await screen.findByRole('status')).toHaveTextContent('点歌数量已达到本房间上限')
  })
})
