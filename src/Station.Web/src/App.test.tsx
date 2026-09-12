import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, vi } from 'vitest'
import { App } from './App'
import { SessionProvider } from './state/session'

const realtime = vi.hoisted(() => ({ snapshot: null as null | { version: number; roomId: string; queue: unknown[]; playback?: unknown }, lastUrl: '', invokeArgs: [] as unknown[] }))
vi.mock('@microsoft/signalr', () => ({
  HubConnectionState: { Disconnected: 'Disconnected' }, LogLevel: { Warning: 3 },
  HubConnectionBuilder: class { withUrl(url: string) { realtime.lastUrl = url; return this } withAutomaticReconnect() { return this } configureLogging() { return this } build() { return { state: 'Connected', on: vi.fn(), off: vi.fn(), onreconnecting: vi.fn(), onreconnected: vi.fn(), onclose: vi.fn(), start: vi.fn().mockResolvedValue(undefined), stop: vi.fn().mockResolvedValue(undefined), invoke: vi.fn().mockImplementation(async (...args: unknown[]) => { realtime.invokeArgs = args; return { version: realtime.snapshot?.version ?? 0, snapshot: realtime.snapshot, events: [] } }) } } },
}))

const validSession = { roomId: 'room-1', roomName: '客厅 KTV', guestId: 'guest-1', nickname: '小明', role: 'Guest' as const, token: 'short-lived-token', expiresAt: '2099-01-01T00:00:00Z' }
function renderApp(path: string) { return render(<MemoryRouter initialEntries={[path]}><SessionProvider><App /></SessionProvider></MemoryRouter>) }

describe('mobile application shell', () => {
  beforeEach(() => { sessionStorage.clear(); realtime.snapshot = { version: 0, roomId: 'room-1', queue: [] }; realtime.lastUrl = ''; realtime.invokeArgs = []; vi.restoreAllMocks(); vi.spyOn(globalThis, 'fetch').mockImplementation(async () => new Response(JSON.stringify({ items: [], total: 0, page: 1, pageSize: 20 }), { status: 200, headers: { 'Content-Type': 'application/json' } })) })
  it('renders join route', () => { renderApp('/join'); expect(screen.getByRole('heading', { name: 'AI-KTV Station' })).toBeInTheDocument(); expect(screen.getByRole('button', { name: '加入房间' })).toBeInTheDocument() })
  it('restores a valid session and renders the approved discovery shell', async () => { sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/discover'); expect(await screen.findByText('小明 · 访客 · 已连接')).toBeInTheDocument(); expect(screen.getByPlaceholderText('搜索歌名、歌手或拼音')).toBeInTheDocument(); expect(screen.getByRole('navigation', { name: '主导航' })).toBeInTheDocument() })
  it('joins a room and persists the short-lived session', async () => {
    vi.mocked(fetch).mockResolvedValue(new Response(JSON.stringify({ ...validSession, guestId: 'guest-1' }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
    renderApp('/join?code=ktv826')
    expect(screen.getByLabelText('房间码')).toHaveValue('KTV826')
    fireEvent.click(screen.getByRole('button', { name: '加入房间' }))
    await screen.findByPlaceholderText('搜索歌名、歌手或拼音')
    expect(JSON.parse(sessionStorage.getItem('ai-ktv-station.room-session.v1') ?? '{}')).toMatchObject({ token: 'short-lived-token', nickname: '小明' })
    expect(fetch).toHaveBeenCalledWith('/api/rooms/join', expect.objectContaining({ method: 'POST' }))
  })
  it('clears the session when the guest exits', async () => { sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/discover'); fireEvent.click(screen.getByRole('button', { name: '退出' })); await waitFor(() => expect(sessionStorage.getItem('ai-ktv-station.room-session.v1')).toBeNull()) })
  it('debounces catalog search and sends selected filters', async () => {
    vi.mocked(fetch).mockImplementation(async () => new Response(JSON.stringify({ items: [{ songId: 'song-1', title: '夜曲', artists: '周杰伦', language: '国语', quality: '4K', availability: 'Available' }], total: 1, page: 1, pageSize: 20 }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
    sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/discover')
    fireEvent.change(screen.getByPlaceholderText('搜索歌名、歌手或拼音'), { target: { value: 'yequ' } })
    fireEvent.click(screen.getByRole('button', { name: '按语种' }))
    fireEvent.click(screen.getByRole('button', { name: '国语' }))
    await screen.findByText('夜曲')
    await waitFor(() => expect(vi.mocked(fetch).mock.calls.some(([url]) => String(url).includes('text=yequ') && String(url).includes(encodeURIComponent('国语')))).toBe(true), { timeout: 1500 })
  })
  it('shows catalog failure and retries', async () => {
    let searches = 0; vi.mocked(fetch).mockImplementation(async url => { const path = String(url); if (path.startsWith('/api/library/favorites')) return new Response(JSON.stringify([]), { status: 200, headers: { 'Content-Type': 'application/json' } }); if (path === '/api/playback') return new Response(JSON.stringify({ state: 'Idle', volume: 80, tracks: [] }), { status: 200, headers: { 'Content-Type': 'application/json' } }); searches++; return searches === 1 ? new Response(JSON.stringify({ title: '曲库不可用', code: 'catalog.offline' }), { status: 503 }) : new Response(JSON.stringify({ items: [], total: 0, page: 1, pageSize: 20 }), { status: 200, headers: { 'Content-Type': 'application/json' } }) })
    sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/discover'); expect(await screen.findByRole('alert')).toHaveTextContent('曲库不可用'); fireEvent.click(screen.getByRole('button', { name: '重试' })); expect(await screen.findByText('没有找到歌曲')).toBeInTheDocument()
  })
  it('navigates between catalog pages', async () => {
    vi.mocked(fetch).mockImplementation(async url => { const page = new URL(String(url), 'http://station').searchParams.get('page'); const item = page === '2' ? { songId: 'song-2', title: '第二首', artists: '歌手乙', availability: 'Available' } : { songId: 'song-1', title: '第一首', artists: '歌手甲', availability: 'Available' }; return new Response(JSON.stringify({ items: [item], total: 2, page: Number(page), pageSize: 1 }), { status: 200, headers: { 'Content-Type': 'application/json' } }) })
    sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/discover'); await screen.findByText('第一首'); fireEvent.click(screen.getByRole('button', { name: '下一页' })); await screen.findByText('第二首'); expect(screen.queryByText('第一首')).not.toBeInTheDocument()
  })
  it('requests a song and reports duplicate requests', async () => {
    const song = { songId: 'song-1', title: '夜曲', artists: '周杰伦', availability: 'Available' }
    let queue: unknown[] = []
    vi.mocked(fetch).mockImplementation(async (url, init) => { const path = String(url); if (path.startsWith('/api/catalog')) return new Response(JSON.stringify({ items: [song], total: 1, page: 1, pageSize: 20 }), { status: 200, headers: { 'Content-Type': 'application/json' } }); if (path === '/api/queue' && init?.method === 'POST') { const item = { id: 'item-1', ...song, requestedByGuestId: 'guest-1', requestedByNickname: '小明', status: 'Waiting' }; queue = [item]; return new Response(JSON.stringify(item), { status: 201, headers: { 'Content-Type': 'application/json' } }) } return new Response(JSON.stringify(queue), { status: 200, headers: { 'Content-Type': 'application/json' } }) })
    sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/discover'); await screen.findByText('夜曲'); fireEvent.click(screen.getByRole('button', { name: '点播 夜曲' })); expect(await screen.findByRole('status')).toHaveTextContent('已点播《夜曲》'); fireEvent.click(screen.getByRole('button', { name: '点播 夜曲' })); expect(await screen.findByRole('status')).toHaveTextContent('已经在你的队列中')
    fireEvent.click(screen.getByRole('link', { name: '已点' })); expect(await screen.findByText('夜曲')).toBeInTheDocument()
    expect(vi.mocked(fetch).mock.calls.filter(([, init]) => init?.method === 'POST')).toHaveLength(1)
  })
  it('lists only my songs and removes a waiting item', async () => {
    const items = [{ id: 'mine-1', songId: 'song-1', title: '我的歌', requestedByGuestId: 'guest-1', requestedByNickname: '小明', position: 1, status: 'Waiting', requestedAt: '2026-09-10T00:00:00Z' }, { id: 'other-1', songId: 'song-2', title: '别人的歌', requestedByGuestId: 'guest-2', requestedByNickname: '朋友', position: 2, status: 'Waiting', requestedAt: '2026-09-10T00:00:00Z' }]
    realtime.snapshot = { version: 5, roomId: 'room-1', queue: items }; vi.mocked(fetch).mockImplementation(async (_url, init) => init?.method === 'DELETE' ? new Response(null, { status: 204 }) : new Response(JSON.stringify(items), { status: 200, headers: { 'Content-Type': 'application/json' } }))
    sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/queue'); await screen.findByText('我的歌'); expect(screen.queryByText('别人的歌')).not.toBeInTheDocument(); expect(screen.getByText('我已点 1 首 · 房间共 2 首')).toBeInTheDocument(); fireEvent.click(screen.getByRole('button', { name: '删除 我的歌' })); await waitFor(() => expect(screen.queryByText('我的歌')).not.toBeInTheDocument())
  })
  it('translates the server-side guest queue limit', async () => {
    const song = { songId: 'song-1', title: '夜曲', artists: '周杰伦', availability: 'Available' }
    vi.mocked(fetch).mockImplementation(async (url, init) => { if (String(url).startsWith('/api/catalog')) return new Response(JSON.stringify({ items: [song], total: 1, page: 1, pageSize: 20 }), { status: 200, headers: { 'Content-Type': 'application/json' } }); if (init?.method === 'POST') return new Response(JSON.stringify({ title: 'Guest queue limit was reached.', code: 'queue.guest_limit_reached' }), { status: 409 }); return new Response(JSON.stringify([]), { status: 200, headers: { 'Content-Type': 'application/json' } }) })
    sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/discover'); await screen.findByText('夜曲'); fireEvent.click(screen.getByRole('button', { name: '点播 夜曲' })); expect(await screen.findByRole('status')).toHaveTextContent('点歌数量已达到本房间上限')
  })
  it('subscribes without putting the token in the hub URL and renders playback controls on their own tab', async () => {
    realtime.snapshot = { version: 8, roomId: 'room-1', queue: [{ id: 'playing-1', songId: 'song-1', title: '正在唱的歌', requestedByGuestId: 'guest-1', requestedByNickname: '小明', position: 1, status: 'Playing', requestedAt: '2026-09-10T00:00:00Z' }], playback: { playbackId: 'p1', state: 'Playing', position: '00:00:03' } }
    sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/playback'); await screen.findByText('正在唱的歌'); expect(screen.getByRole('button', { name: '暂停' })).toBeInTheDocument(); expect(screen.getByRole('button', { name: '切歌' })).toBeInTheDocument(); expect(realtime.lastUrl).toBe('/hubs/room'); expect(realtime.lastUrl).not.toContain('short-lived-token'); expect(realtime.invokeArgs).toEqual(['Subscribe', 'short-lived-token', null])
  })
  it('toggles a favorite from discovery', async () => {
    const song = { songId: 'song-1', title: '收藏测试歌', artists: '歌手', availability: 'Available' }
    vi.mocked(fetch).mockImplementation(async (url) => String(url).startsWith('/api/catalog') ? new Response(JSON.stringify({ items: [song], total: 1, page: 1, pageSize: 20 }), { status: 200, headers: { 'Content-Type': 'application/json' } }) : new Response(JSON.stringify([]), { status: 200, headers: { 'Content-Type': 'application/json' } }))
    sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/discover'); await screen.findByText('收藏测试歌'); fireEvent.click(screen.getByRole('button', { name: '收藏 收藏测试歌' })); expect(await screen.findByRole('status')).toHaveTextContent('已收藏《收藏测试歌》'); expect(vi.mocked(fetch).mock.calls.some(([url, init]) => String(url).includes('/api/library/favorites/song-1') && init?.method === 'PUT')).toBe(true)
  })
  it('uses independent second-level singer language and style menus instead of combined selects', async () => {
    sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/discover')
    expect(await screen.findByRole('button', { name: '按歌星' })).toBeInTheDocument(); expect(screen.getByRole('button', { name: '按语种' })).toBeInTheDocument(); expect(screen.getByRole('button', { name: '按风格' })).toBeInTheDocument(); expect(screen.queryByRole('combobox')).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: '按语种' })); expect(screen.getByRole('button', { name: '粤语' })).toBeInTheDocument(); expect(screen.queryByRole('button', { name: '流行' })).not.toBeInTheDocument()
  })
  it('shows singer cards and searches all songs by the selected exact artist', async () => {
    vi.mocked(fetch).mockImplementation(async url => String(url).startsWith('/api/catalog/artists') ? new Response(JSON.stringify([{ artistId: 'artist-1', name: '周杰伦', songCount: 2 }]), { status: 200, headers: { 'Content-Type': 'application/json' } }) : new Response(JSON.stringify({ items: [], total: 0, page: 1, pageSize: 20 }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
    sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/discover'); await screen.findByRole('button', { name: '按歌星' }); fireEvent.click(screen.getByRole('button', { name: '按歌星' })); const catalogMenu = within(screen.getByRole('region', { name: '曲库分类' })); fireEvent.click(catalogMenu.getByRole('button', { name: '全部' })); fireEvent.click(await screen.findByRole('button', { name: '周杰伦' }));
    await waitFor(() => expect(vi.mocked(fetch).mock.calls.some(([url]) => String(url).includes(`artist=${encodeURIComponent('周杰伦')}`))).toBe(true))
  })
  it('inserts an owned queued song without a full page refresh', async () => {
    const item = { id: 'mine-1', songId: 'song-1', title: '我的歌', requestedByGuestId: 'guest-1', requestedByNickname: '小明', position: 1024, status: 'Waiting', requestedAt: '2026-09-10T00:00:00Z' }
    realtime.snapshot = { version: 5, roomId: 'room-1', queue: [item] }; vi.mocked(fetch).mockImplementation(async (url, init) => String(url).endsWith('/insert') && init?.method === 'POST' ? new Response(JSON.stringify({ ...item, position: 0 }), { status: 200, headers: { 'Content-Type': 'application/json' } }) : new Response(JSON.stringify([item]), { status: 200, headers: { 'Content-Type': 'application/json' } }))
    sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/queue'); await screen.findByText('我的歌'); fireEvent.click(screen.getByRole('button', { name: '插播 我的歌' })); await waitFor(() => expect(vi.mocked(fetch).mock.calls.some(([url, init]) => String(url).endsWith('/insert') && init?.method === 'POST')).toBe(true))
  })
  it('replaces the unused my tab with a playback tab', async () => { sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/discover'); expect(await screen.findByRole('link', { name: '播放' })).toBeInTheDocument(); expect(screen.queryByRole('link', { name: '我的' })).not.toBeInTheDocument() })
  it('loads the favorites page and removes a favorite', async () => {
    vi.mocked(fetch).mockImplementation(async (_url, init) => init?.method === 'PUT' ? new Response(JSON.stringify({ favorite: false }), { status: 200, headers: { 'Content-Type': 'application/json' } }) : new Response(JSON.stringify([{ songId: 'fav-1', title: '心爱歌曲', artists: '歌手', favoritedAt: '2026-09-10T00:00:00Z' }]), { status: 200, headers: { 'Content-Type': 'application/json' } }))
    sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/favorites'); await screen.findByText('心爱歌曲'); fireEvent.click(screen.getByRole('button', { name: '取消收藏 心爱歌曲' })); await waitFor(() => expect(screen.queryByText('心爱歌曲')).not.toBeInTheDocument())
  })
})
