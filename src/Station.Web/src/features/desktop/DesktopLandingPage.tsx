import {
  Copy,
  Home,
  Library,
  ListMusic,
  Mic2,
  MonitorPlay,
  Music2,
  Pause,
  Play,
  QrCode,
  Search,
  Settings,
  SkipForward,
  Users,
  Volume2,
} from 'lucide-react'
import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { ApiError } from '../../api/client'
import { RoomRealtimeProvider, useRoomRealtime } from '../../realtime/room-realtime'
import { useSession, type RoomRole } from '../../state/session'
import type { QueueEntry } from '../queue/types'

interface HostRoomResponse {
  room: { id: string; joinCode: string; maxQueuedSongsPerGuest: number }
  host: { token: string; roomId: string; guestId: string; nickname: string; role: RoomRole; expiresAt: string }
  joinUrl?: string
}

interface SongSearchItem {
  songId: string
  title: string
  artists: string
  language?: string
  category?: string
  quality?: string
  availability: 'Available' | 'Offline' | 'Unreadable'
}

interface SongSearchPage { items: SongSearchItem[]; total: number; page: number; pageSize: number }
interface FavoriteSong { songId: string; title: string; artists: string; favoritedAt: string }
interface ArtistItem { artistId?: string; id?: string; name: string; songCount: number; imageUrl?: string; avatarUrl?: string }
type DesktopView = 'songs' | 'artists' | 'language' | 'style' | 'favorites'
type SearchField = 'Any' | 'Title' | 'Artist'

const singerGroups = ['全部', '华语男歌手', '华语女歌手', '华语组合', '欧美歌手', '日本歌手', '韩国歌手', '其他']
const languages = ['全部', '国语', '粤语', '台语', '闽南语', '英语', '日语', '韩语', '纯音乐']
const styles = ['全部', '流行', '经典', '摇滚', '民谣', '儿歌', '舞曲', '影视原声', '纯音乐']
const statusLabels: Record<QueueEntry['status'], string> = {
  Probing: '正在探测', ProbeFailed: '探测失败', Waiting: '等待播放', Preparing: '准备播放',
  Playing: '正在播放', Paused: '已暂停', Completed: '已播放', Skipped: '已跳过', Failed: '播放失败',
}

function avatarColor(name: string) {
  const colors = ['#f56a00', '#7265e6', '#00a2ae', '#1890ff', '#52c41a', '#eb2f96', '#d58c4e']
  let hash = 0
  for (const character of name) hash = character.charCodeAt(0) + ((hash << 5) - hash)
  return colors[Math.abs(hash) % colors.length]
}

function formatClock(value?: string) {
  if (!value) return '--:--'
  const number = Number(value)
  if (Number.isFinite(number)) {
    const seconds = Math.max(0, Math.floor(number))
    return `${String(Math.floor(seconds / 60)).padStart(2, '0')}:${String(seconds % 60).padStart(2, '0')}`
  }
  const match = value.match(/^(?:\d+\.)?(\d{2}:\d{2}:\d{2})/)
  return match ? match[1].slice(3) : value.slice(-5)
}

export function DesktopLandingPage() {
  const { api, setSession } = useSession()
  const [room, setRoom] = useState<HostRoomResponse | null>(null)
  const [error, setError] = useState('')
  const initialized = useRef(false)

  useEffect(() => {
    if (initialized.current) return
    initialized.current = true
    const controller = new AbortController()
    api.post<HostRoomResponse>('/api/manage/room/ensure', { hostNickname: '主持人', maxQueuedSongsPerGuest: 100 }, controller.signal)
      .then(result => { setRoom(result); setSession({ ...result.host, roomName: '客厅 KTV' }) })
      .catch(value => {
        if (!(value instanceof DOMException && value.name === 'AbortError')) setError(value instanceof ApiError ? value.message : '无法在本机创建 KTV 房间。')
      })
    return () => controller.abort()
  }, [api, setSession])

  if (error) return <main className="desk-loading-shell"><section className="desk-error"><Music2 /><h1>主控未就绪</h1><p>{error}</p><button onClick={() => window.location.reload()}>重试</button></section></main>
  if (!room) return <main className="desk-loading-shell"><section className="desk-loading"><Music2 /><p>正在恢复家庭 KTV 房间…</p></section></main>
  return <RoomRealtimeProvider><DesktopRoomPage room={room.room} joinUrl={room.joinUrl} /></RoomRealtimeProvider>
}

function DesktopRoomPage({ room, joinUrl: preferredJoinUrl }: { room: HostRoomResponse['room']; joinUrl?: string }) {
  const { api } = useSession()
  const { queue, playback, connectionStatus, addQueueItem, updateQueueItem } = useRoomRealtime()
  const [view, setView] = useState<DesktopView>('songs')
  const [searchText, setSearchText] = useState('周杰伦')
  const [submittedText, setSubmittedText] = useState('周杰伦')
  const [searchField, setSearchField] = useState<SearchField>('Title')
  const [language, setLanguage] = useState('')
  const [style, setStyle] = useState('')
  const [artistGroup, setArtistGroup] = useState('')
  const [page, setPage] = useState(1)
  const [result, setResult] = useState<SongSearchPage | null>(null)
  const [artists, setArtists] = useState<ArtistItem[]>([])
  const [favorites, setFavorites] = useState<FavoriteSong[]>([])
  const [loading, setLoading] = useState(false)
  const [artistLoading, setArtistLoading] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [requestingId, setRequestingId] = useState('')
  const [pendingControl, setPendingControl] = useState('')
  const [copied, setCopied] = useState(false)

  const waitingQueue = useMemo(() => queue.filter(item => !['Completed', 'Skipped', 'Failed', 'Playing', 'Paused'].includes(item.status)).sort((a, b) => a.position - b.position), [queue])
  const currentSong = useMemo(() => queue.find(item => item.status === 'Playing' || item.status === 'Paused') ?? queue.find(item => item.status === 'Preparing'), [queue])
  const totalPages = result ? Math.max(1, Math.ceil(result.total / result.pageSize)) : 1
  const joinUrl = preferredJoinUrl ?? `${window.location.origin}/join?code=${encodeURIComponent(room.joinCode)}`
  const filterOptions = view === 'artists' ? singerGroups : view === 'language' ? languages : view === 'style' ? styles : []

  useEffect(() => {
    const controller = new AbortController()
    api.get<FavoriteSong[]>('/api/library/favorites', controller.signal).then(setFavorites).catch(() => undefined)
    return () => controller.abort()
  }, [api])

  useEffect(() => {
    if (view !== 'artists') return
    const controller = new AbortController()
    const query = artistGroup ? `?artistGroup=${encodeURIComponent(artistGroup)}` : ''
    setArtistLoading(true)
    api.get<ArtistItem[]>(`/api/catalog/artists${query}`, controller.signal)
      .then(items => setArtists(items ?? []))
      .catch(value => { if (!(value instanceof DOMException && value.name === 'AbortError')) setError('歌手列表暂时无法访问。') })
      .finally(() => { if (!controller.signal.aborted) setArtistLoading(false) })
    return () => controller.abort()
  }, [api, artistGroup, view])

  useEffect(() => {
    if (view === 'artists' || view === 'favorites') return
    const controller = new AbortController()
    setLoading(true); setError('')
    const params = new URLSearchParams({ page: String(page), pageSize: '20', sort: 'Relevance' })
    if (submittedText) params.set('text', submittedText)
    if (searchField !== 'Any') params.set('field', searchField)
    if (language) params.set('language', language)
    if (style) params.set('category', style)
    api.get<SongSearchPage>(`/api/catalog/search?${params}`, controller.signal)
      .then(next => setResult(next))
      .catch(value => { if (!(value instanceof DOMException && value.name === 'AbortError')) setError(value instanceof ApiError ? value.message : '曲库暂时无法访问。') })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [api, language, page, searchField, style, submittedText, view])

  function selectView(next: DesktopView) {
    setView(next); setError(''); setPage(1)
    if (next === 'artists') setArtistGroup('')
    if (next === 'songs') { setLanguage(''); setStyle('') }
  }

  function selectFilter(value: string) {
    if (view === 'artists') setArtistGroup(value === '全部' ? '' : value)
    else if (view === 'language') { setLanguage(value === '全部' ? '' : value); setStyle(''); setPage(1) }
    else if (view === 'style') { setStyle(value === '全部' ? '' : value); setLanguage(''); setPage(1) }
  }

  function submitSearch(event?: FormEvent) {
    event?.preventDefault()
    setSubmittedText(searchText.trim())
    setPage(1)
    setView('songs')
  }

  async function requestSong(song: SongSearchItem | FavoriteSong) {
    if (requestingId) return
    if (queue.some(item => item.songId === song.songId && !['Completed', 'Skipped', 'Failed'].includes(item.status))) { setNotice(`《${song.title}》已经在队列中。`); return }
    setRequestingId(song.songId); setNotice('')
    try {
      const queued = await api.post<QueueEntry>('/api/queue', { songId: song.songId })
      addQueueItem(queued); setNotice(`已点播《${song.title}》`)
    } catch (value) { setNotice(value instanceof ApiError ? value.message : '点歌失败，请稍后重试。') }
    finally { setRequestingId('') }
  }

  async function queueInsert(item: QueueEntry) {
    if (pendingControl) return
    setPendingControl(item.id); setNotice('')
    try { updateQueueItem(await api.post<QueueEntry>(`/api/queue/${item.id}/insert`, {})); setNotice(`《${item.title}》已插入当前播放的下一首。`) }
    catch (value) { setNotice(value instanceof ApiError ? value.message : '无法插播这首歌。') }
    finally { setPendingControl('') }
  }

  async function playbackControl(action: 'play' | 'pause' | 'skip') {
    if (pendingControl) return
    setPendingControl(action); setNotice('')
    try { await api.post(`/api/playback/${action}`, {}); }
    catch (value) { setNotice(value instanceof ApiError ? value.message : '播放控制失败。') }
    finally { setPendingControl('') }
  }

  async function copyJoinUrl() {
    try { await navigator.clipboard.writeText(joinUrl); setCopied(true); window.setTimeout(() => setCopied(false), 1600) }
    catch { setNotice(`请手动输入房间码 ${room.joinCode}`) }
  }

  function chooseArtist(artist: ArtistItem) {
    setSearchText(artist.name); setSubmittedText(artist.name); setSearchField('Artist'); setLanguage(''); setStyle(''); setPage(1); setView('songs')
  }

  const songs = view === 'favorites' ? favorites : result?.items ?? []
  const resultTitle = view === 'artists' ? (artistGroup || '全部') + '歌手' : view === 'favorites' ? '我的收藏' : view === 'language' && language ? `${language}歌曲` : view === 'style' && style ? `${style}歌曲` : searchField === 'Artist' && submittedText ? `${submittedText}的歌曲` : '搜索结果'
  const resultCount = view === 'artists' ? `${artists.length} 位歌手` : view === 'favorites' ? `${favorites.length} 首` : result ? `找到 ${result.total} 首 · 第 ${page} / ${totalPages} 页` : loading ? '加载中…' : '暂无结果'
  const connectionText = { connecting: '连接中', connected: '服务在线', reconnecting: '重新连接中', offline: '连接断开' }[connectionStatus]

  return <div className="ktv-app">
    <div className="ktv-window">
      <aside className="ktv-sidebar">
        <div className="ktv-brand"><span className="ktv-logo"><Music2 aria-hidden="true" /></span><span className="ktv-brand-copy"><strong>AI-KTV</strong><small>STATION 主控</small></span></div>
        <nav className="ktv-nav" aria-label="主控导航">
          <button type="button" onClick={() => setNotice('主控总览正在使用当前房间数据')}><Home aria-hidden="true" /><span>总览</span></button>
          <button type="button" onClick={() => document.getElementById('ktv-now-playing')?.scrollIntoView({ behavior: 'smooth' })}><Play aria-hidden="true" /><span>正在播放</span></button>
          <button type="button" className="active" onClick={() => selectView('songs')}><Search aria-hidden="true" /><span>电脑点歌</span></button>
          <button type="button" onClick={() => document.getElementById('ktv-queue')?.scrollIntoView({ behavior: 'smooth' })}><ListMusic aria-hidden="true" /><span>点歌队列</span></button>
          <button type="button" onClick={() => setNotice('曲库管理请在设置与诊断中操作')}><Library aria-hidden="true" /><span>曲库管理</span></button>
          <button type="button" onClick={() => setNotice(`房间码 ${room.joinCode}，可复制加入链接`)}><QrCode aria-hidden="true" /><span>房间与二维码</span></button>
          <button type="button" onClick={() => setNotice('设置入口保留在托盘启动器中')}><Settings aria-hidden="true" /><span>设置与诊断</span></button>
        </nav>
        <div className="ktv-side-status"><strong><span className="ktv-online-dot"></span>{connectionText}</strong><div className="ktv-side-status-copy">队列 {waitingQueue.length} 首 · 房间码 {room.joinCode}</div></div>
      </aside>

      <main className="ktv-main">
        <header className="ktv-header">
          <div><h1>电脑点歌</h1><p>主控机直接搜歌、点歌和管理当前队列</p></div>
          <div className="ktv-room"><span className="ktv-room-name">客厅 KTV · {room.joinCode}</span><span className="ktv-room-live">● 房间已开启</span></div>
        </header>

        <div className="ktv-layout">
          <section className="ktv-catalog" aria-label="电脑点歌区">
            <form className="ktv-search" onSubmit={submitSearch}>
              <label className="ktv-search-input"><Search aria-hidden="true" /><span className="sr-only">搜索歌曲</span><input type="search" value={searchText} onChange={event => setSearchText(event.target.value)} placeholder="输入歌名、歌手或拼音" /></label>
              <button type="submit" className={searchField === 'Title' ? 'primary' : ''} onClick={() => setSearchField('Title')}>按歌名</button>
              <button type="submit" className={searchField === 'Artist' ? 'primary' : ''} onClick={() => setSearchField('Artist')}>按歌手</button>
            </form>

            <div className="ktv-tabs" role="tablist" aria-label="点歌浏览方式">
              {([['songs', '歌曲'], ['artists', '歌星'], ['language', '语种'], ['style', '风格'], ['favorites', '收藏']] as const).map(([value, label]) => <button type="button" role="tab" aria-selected={view === value} key={value} className={view === value ? 'active' : ''} onClick={() => selectView(value)}>{label}</button>)}
            </div>

            {filterOptions.length > 0 && <div className="ktv-artist-filters" aria-label="二级筛选">{filterOptions.map(value => {
              const selected = view === 'artists' ? (value === '全部' ? artistGroup === '' : artistGroup === value) : view === 'language' ? (value === '全部' ? language === '' : language === value) : (value === '全部' ? style === '' : style === value)
              return <button type="button" key={value} className={selected ? 'active' : ''} onClick={() => selectFilter(value)}>{value}</button>
            })}</div>}

            <div className="ktv-section-head"><strong>{resultTitle}</strong><span>{resultCount}</span></div>

            {view === 'artists' ? <div className="ktv-artist-grid" aria-label="歌手列表">{artistLoading ? <p className="ktv-empty">正在加载歌手…</p> : artists.length === 0 ? <p className="ktv-empty">该分类下暂无歌手数据</p> : artists.map(artist => {
              const image = artist.imageUrl ?? artist.avatarUrl
              return <button type="button" className="ktv-artist" key={artist.artistId ?? artist.id ?? artist.name} onClick={() => chooseArtist(artist)}><span className="ktv-avatar" style={{ background: image ? 'transparent' : `linear-gradient(145deg, ${avatarColor(artist.name)}, #24133f)` }}>{image ? <img src={image} alt="" onError={event => { event.currentTarget.style.display = 'none' }} /> : artist.name.trim().slice(0, 1)}</span><span>{artist.name}</span><small>{artist.songCount} 首</small></button>
            })}</div> : <div className="ktv-song-list" aria-label="歌曲列表">{loading && !result && <p className="ktv-empty">正在搜索曲库…</p>}{!loading && songs.length === 0 && <p className="ktv-empty">没有找到歌曲</p>}{songs.map((song, index) => <div className="ktv-song-row" key={song.songId}><span className="ktv-song-index">{String((page - 1) * 20 + index + 1).padStart(2, '0')}</span><span className="ktv-song-copy"><strong>{song.title}</strong><small>{song.artists || '未知歌手'}{('language' in song && song.language) ? ` · ${song.language}` : ''}{('category' in song && song.category) ? ` · ${song.category}` : ''}</small></span>{'quality' in song && song.quality && <span className="ktv-quality">{song.quality}</span>}<button type="button" className={queue.some(item => item.songId === song.songId) ? 'ktv-request queued' : 'ktv-request'} disabled={requestingId === song.songId || ('availability' in song && song.availability !== 'Available')} onClick={() => void requestSong(song)}>{requestingId === song.songId ? '…' : queue.some(item => item.songId === song.songId) ? '已点' : '点歌'}</button></div>)}{view !== 'favorites' && result && <div className="ktv-pagination"><button type="button" disabled={loading || page <= 1} onClick={() => setPage(value => Math.max(1, value - 1))}>上一页</button><span>第 {page} / {totalPages} 页</span><button type="button" disabled={loading || page >= totalPages} onClick={() => setPage(value => Math.min(totalPages, value + 1))}>下一页</button></div>}</div>}
            {notice && <p className="ktv-notice" role="status">{notice}</p>}
          </section>

          <aside className="ktv-queue" id="ktv-queue" aria-label="播放与队列">
            <section className="ktv-now" id="ktv-now-playing">
              <span className="ktv-now-label">NOW PLAYING</span><strong>{currentSong?.title ?? '暂无歌曲'}</strong><small>{currentSong?.artists || '等待点歌'} · {playback?.state === 'Paused' ? '已暂停' : playback?.state === 'Playing' ? '播放中' : '等待播放'}</small>
              <div className="ktv-progress"><span style={{ width: playback?.duration && playback.position ? `${Math.min(100, Math.max(0, Number(playback.position) / Math.max(1, Number(playback.duration)) * 100))}%` : '0%' }}></span></div><div className="ktv-time"><span>{formatClock(playback?.position)}</span><span>{formatClock(playback?.duration)}</span></div>
              <div className="ktv-player-actions"><button type="button" aria-label={playback?.state === 'Playing' ? '暂停' : '播放'} disabled={pendingControl !== ''} onClick={() => void playbackControl(playback?.state === 'Playing' ? 'pause' : 'play')}>{playback?.state === 'Playing' ? <Pause aria-hidden="true" /> : <Play aria-hidden="true" />}</button><button type="button" aria-label="切歌" disabled={pendingControl !== '' || !currentSong} onClick={() => void playbackControl('skip')}><SkipForward aria-hidden="true" /></button><button type="button" aria-label="原唱伴奏" onClick={() => setNotice('请在播放器页面选择原唱或伴奏音轨')}><Mic2 aria-hidden="true" /></button><button type="button" aria-label="音量" onClick={() => setNotice('请在播放器页面调整音量')}><Volume2 aria-hidden="true" /></button></div>
            </section>
            <div className="ktv-queue-head"><strong>接下来播放</strong><span>{waitingQueue.length} 首</span></div>
            <div className="ktv-queue-list">{waitingQueue.length === 0 ? <p className="ktv-empty">队列为空</p> : waitingQueue.map((item, index) => <div className="ktv-queue-row" key={item.id}><span>{index + 1}</span><span className="ktv-queue-copy"><strong>{item.title}</strong><small>{item.artists || item.requestedByNickname} · {statusLabels[item.status]}</small></span><button type="button" className="ktv-insert" aria-label={`插播${item.title}`} disabled={pendingControl !== '' || item.status === 'ProbeFailed'} onClick={() => void queueInsert(item)}><MonitorPlay aria-hidden="true" /></button></div>)}</div>
            <div className="ktv-room-tools"><QrCode aria-hidden="true" /><div><strong>手机加入</strong><small>房间码 {room.joinCode}</small></div><button type="button" onClick={() => void copyJoinUrl()}><Copy aria-hidden="true" />{copied ? '已复制' : '复制链接'}</button></div>
          </aside>
        </div>
      </main>
    </div>
  </div>
}
