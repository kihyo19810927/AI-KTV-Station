import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, vi } from 'vitest'
import { App } from './App'
import { SessionProvider } from './state/session'

const validSession = { roomId: 'room-1', roomName: '客厅 KTV', nickname: '小明', role: 'Guest' as const, token: 'short-lived-token', expiresAt: '2099-01-01T00:00:00Z' }
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
})
