import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, vi } from 'vitest'
import { App } from './App'
import { SessionProvider } from './state/session'

const validSession = { roomId: 'room-1', roomName: '客厅 KTV', nickname: '小明', role: 'Guest' as const, token: 'short-lived-token', expiresAt: '2099-01-01T00:00:00Z' }
function renderApp(path: string) { return render(<MemoryRouter initialEntries={[path]}><SessionProvider><App /></SessionProvider></MemoryRouter>) }

describe('mobile application shell', () => {
  beforeEach(() => { sessionStorage.clear(); vi.restoreAllMocks() })
  it('renders join route', () => { renderApp('/join'); expect(screen.getByRole('heading', { name: 'AI-KTV Station' })).toBeInTheDocument(); expect(screen.getByRole('button', { name: '加入房间' })).toBeInTheDocument() })
  it('restores a valid session and renders the approved discovery shell', () => { sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/discover'); expect(screen.getByText('小明 · 访客')).toBeInTheDocument(); expect(screen.getByPlaceholderText('搜索歌名、歌手或拼音')).toBeInTheDocument(); expect(screen.getByRole('navigation', { name: '主导航' })).toBeInTheDocument() })
  it('joins a room and persists the short-lived session', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({ ...validSession, guestId: 'guest-1' }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
    renderApp('/join?code=ktv826')
    expect(screen.getByLabelText('房间码')).toHaveValue('KTV826')
    fireEvent.change(screen.getByLabelText('昵称'), { target: { value: '小明' } })
    fireEvent.click(screen.getByRole('button', { name: '加入房间' }))
    await screen.findByPlaceholderText('搜索歌名、歌手或拼音')
    expect(JSON.parse(sessionStorage.getItem('ai-ktv-station.room-session.v1') ?? '{}')).toMatchObject({ token: 'short-lived-token', nickname: '小明' })
    expect(fetch).toHaveBeenCalledWith('/api/rooms/join', expect.objectContaining({ method: 'POST' }))
  })
  it('clears the session when the guest exits', async () => { sessionStorage.setItem('ai-ktv-station.room-session.v1', JSON.stringify(validSession)); renderApp('/room/discover'); fireEvent.click(screen.getByRole('button', { name: '退出' })); await waitFor(() => expect(sessionStorage.getItem('ai-ktv-station.room-session.v1')).toBeNull()) })
})
