import { ChevronLeft, Heart, Languages, Music2, Search, Shapes, Users } from 'lucide-react'
import { useEffect, useMemo, useRef, useState } from 'react'
import { ApiError } from '../../api/client'
import { useRoomRealtime } from '../../realtime/room-realtime'
import { useSession } from '../../state/session'
import type { QueueEntry } from '../queue/types'

interface SongSearchItem { songId: string; title: string; artists: string; language?: string; category?: string; quality?: string; year?: number; availability: 'Available' | 'Offline' | 'Unreadable' }
interface SongSearchPage { items: SongSearchItem[]; total: number; page: number; pageSize: number }
interface Filters { artistGroup: string; language: string; category: string; sort: string }
interface FavoriteSong { songId: string }
interface ArtistItem { id?: string; artistId?: string; name: string; songCount: number; imageUrl?: string; avatarUrl?: string }
type BrowseMode = 'root' | 'singer-groups' | 'singers' | 'artist-songs' | 'languages' | 'styles'
type SearchField = 'Any' | 'Title' | 'Artist'

const initialFilters: Filters = { artistGroup: '', language: '', category: '', sort: 'Relevance' }
const singerGroups = ['全部', '华语男歌手', '华语女歌手', '华语组合', '欧美歌手', '日本歌手', '韩国歌手', '其他']
const languages = ['全部', '国语', '粤语', '台语', '闽南语', '英语', '日语', '韩语', '纯音乐']
const styles = ['全部', '流行', '经典', '摇滚', '民谣', '儿歌', '舞曲', '影视原声', '纯音乐']
const pageSize = 50

function avatarColor(name: string) {
  const colors = ['#f56a00', '#7265e6', '#ffbf00', '#00a2ae', '#1890ff', '#52c41a', '#eb2f96']
  let hash = 0
  for (const character of name) hash = character.charCodeAt(0) + ((hash << 5) - hash)
  return colors[Math.abs(hash) % colors.length]
}

interface SavedDiscoverState {
  text: string
  submittedText: string
  searchField: SearchField
  filters: Filters
  page: number
  browseMode: BrowseMode
  artistGroup: string
  selectedArtist: string
}

function loadDiscoverState(key: string): SavedDiscoverState | null {
  try {
    const raw = sessionStorage.getItem(key)
    if (!raw) return null
    const value = JSON.parse(raw) as Partial<SavedDiscoverState>
    if (typeof value.text !== 'string' || typeof value.submittedText !== 'string') return null
    return {
      text: value.text,
      submittedText: value.submittedText,
      searchField: value.searchField === 'Title' || value.searchField === 'Artist' || value.searchField === 'Any' ? value.searchField : 'Any',
      filters: { ...initialFilters, ...(value.filters ?? {}) },
      page: typeof value.page === 'number' && value.page > 0 ? Math.floor(value.page) : 1,
      browseMode: value.browseMode === 'singer-groups' || value.browseMode === 'singers' || value.browseMode === 'artist-songs' || value.browseMode === 'languages' || value.browseMode === 'styles' ? value.browseMode : 'root',
      artistGroup: typeof value.artistGroup === 'string' ? value.artistGroup : '',
      selectedArtist: typeof value.selectedArtist === 'string' ? value.selectedArtist : '',
    }
  } catch { return null }
}

export function DiscoverPage() {
  const { api, session } = useSession()
  const { addQueueItem } = useRoomRealtime()
  const storageKey = `ai-ktv-station.discover-state.v1.${session?.roomId ?? 'unknown'}.${session?.guestId ?? 'unknown'}`
  const [savedState] = useState(() => loadDiscoverState(storageKey))
  const [text, setText] = useState(savedState?.text ?? '')
  const [submittedText, setSubmittedText] = useState(savedState?.submittedText ?? '')
  const [searchField, setSearchField] = useState<SearchField>(savedState?.searchField ?? 'Any')
  const [filters, setFilters] = useState<Filters>(savedState?.filters ?? initialFilters)
  const [page, setPage] = useState(savedState?.page ?? 1)
  const [result, setResult] = useState<SongSearchPage | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [retry, setRetry] = useState(0)
  const [requestingSongId, setRequestingSongId] = useState('')
  const [notice, setNotice] = useState('')
  const [favorites, setFavorites] = useState<Set<string>>(new Set())
  const [browseMode, setBrowseMode] = useState<BrowseMode>(savedState?.browseMode ?? 'root')
  const [artists, setArtists] = useState<ArtistItem[]>([])
  const [artistLoading, setArtistLoading] = useState(false)
  const [artistGroup, setArtistGroup] = useState(savedState?.artistGroup ?? '')
  const [selectedArtist, setSelectedArtist] = useState(savedState?.selectedArtist ?? '')
  const [animatedSongId, setAnimatedSongId] = useState('')
  const requestSequence = useRef(0)

  const queryKey = useMemo(() => JSON.stringify([submittedText, searchField, filters, selectedArtist]), [submittedText, searchField, filters, selectedArtist])
  const isArtistBrowse = browseMode === 'singer-groups' || browseMode === 'singers'
  const totalPages = result ? Math.max(1, Math.ceil(result.total / result.pageSize)) : 1

  useEffect(() => {
    const controller = new AbortController()
    api.get<FavoriteSong[]>('/api/library/favorites', controller.signal).then(items => setFavorites(new Set(items.map(item => item.songId)))).catch(() => undefined)
    return () => controller.abort()
  }, [api])

  useEffect(() => {
    try {
      sessionStorage.setItem(storageKey, JSON.stringify({ text, submittedText, searchField, filters, page, browseMode, artistGroup, selectedArtist } satisfies SavedDiscoverState))
    } catch { /* session storage is optional */ }
  }, [storageKey, text, submittedText, searchField, filters, page, browseMode, artistGroup, selectedArtist])

  useEffect(() => {
    if (browseMode !== 'singers') return
    const controller = new AbortController()
    const params = artistGroup ? `?artistGroup=${encodeURIComponent(artistGroup)}` : ''
    setArtistLoading(true)
    api.get<ArtistItem[]>(`/api/catalog/artists${params}`, controller.signal).then(items => setArtists(items ?? [])).catch(() => setArtists([])).finally(() => setArtistLoading(false))
    return () => controller.abort()
  }, [api, artistGroup, browseMode])

  useEffect(() => { setPage(1) }, [queryKey])

  useEffect(() => {
    const controller = new AbortController()
    const sequence = ++requestSequence.current
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize), sort: filters.sort })
    if (submittedText) params.set('text', submittedText)
    if (searchField !== 'Any') params.set('field', searchField)
    if (filters.language) params.set('language', filters.language)
    if (filters.category) params.set('category', filters.category)
    if (filters.artistGroup) params.set('artistGroup', filters.artistGroup)
    if (selectedArtist) params.set('artist', selectedArtist)
    setLoading(true); setError('')
    api.get<SongSearchPage>(`/api/catalog/search?${params}`, controller.signal)
      .then(next => { if (sequence === requestSequence.current) setResult(next) })
      .catch(value => { if (sequence === requestSequence.current && !(value instanceof DOMException && value.name === 'AbortError')) setError(value instanceof ApiError ? value.message : '曲库暂时无法访问，请稍后重试。') })
      .finally(() => { if (sequence === requestSequence.current && !controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [api, submittedText, searchField, filters, selectedArtist, page, retry])

  const submitSearch = (field: SearchField) => {
    setSearchField(field)
    setSubmittedText(text.trim())
    setPage(1)
    if (browseMode !== 'root' && browseMode !== 'artist-songs') setBrowseMode('root')
  }

  const chooseCategory = (name: 'language' | 'category', value: string) => {
    setSelectedArtist('')
    setFilters({ ...initialFilters, [name]: value === '全部' ? '' : value })
    setBrowseMode('root')
  }

  async function requestSong(song: SongSearchItem) {
    setRequestingSongId(song.songId); setNotice('')
    try {
      const queue = await api.get<QueueEntry[]>('/api/queue')
      if (queue.some(item => item.songId === song.songId && item.requestedByGuestId === session?.guestId)) { setNotice(`《${song.title}》已经在你的队列中。`); return }
      const queued = await api.post<QueueEntry>('/api/queue', { songId: song.songId })
      addQueueItem(queued)
      setAnimatedSongId(song.songId)
      window.setTimeout(() => setAnimatedSongId(current => current === song.songId ? '' : current), 650)
      setNotice(`已点播《${song.title}》，正在探测媒体。`)
    } catch (value) {
      if (value instanceof ApiError && value.problem.code === 'queue.guest_limit_reached') setNotice('你的点歌数量已达到本房间上限。')
      else setNotice(value instanceof ApiError ? value.message : '点歌失败，请稍后重试。')
    } finally { setRequestingSongId('') }
  }

  async function toggleFavorite(song: SongSearchItem) {
    const favorite = !favorites.has(song.songId)
    try {
      await api.put(`/api/library/favorites/${song.songId}`, { favorite })
      setFavorites(current => { const next = new Set(current); if (favorite) next.add(song.songId); else next.delete(song.songId); return next })
      setNotice(favorite ? `已收藏《${song.title}》。` : `已取消收藏《${song.title}》。`)
    } catch (value) { setNotice(value instanceof ApiError ? value.message : '收藏操作失败。') }
  }

  function chooseArtist(artist: ArtistItem) {
    setText(''); setSubmittedText(''); setSearchField('Artist'); setSelectedArtist(artist.name); setFilters(initialFilters); setBrowseMode('artist-songs')
  }

  return <>
    <label className="search-box">
      <Search aria-hidden="true" /><span className="sr-only">搜索歌曲</span>
      <input value={text} onChange={event => setText(event.target.value)} placeholder="输入关键字后点击按歌名或按歌手" />
    </label>
    <div className="search-scope" role="group" aria-label="搜索范围">
      {([['Any', '全部'], ['Title', '按歌名'], ['Artist', '按歌手']] as const).map(([value, label]) => <button key={value} className={searchField === value ? 'active' : ''} onClick={() => submitSearch(value)}>{label}</button>)}
    </div>

    <section className="browse-menu" aria-label="曲库分类">
      {browseMode === 'root' && <div className="browse-root"><button onClick={() => { setSelectedArtist(''); setArtistGroup(''); setBrowseMode('singers') }}><Users />按歌星</button><button onClick={() => setBrowseMode('languages')}><Languages />按语种</button><button onClick={() => setBrowseMode('styles')}><Shapes />按风格</button></div>}
      {browseMode !== 'root' && <button className="browse-back" onClick={() => setBrowseMode(browseMode === 'artist-songs' ? 'singers' : 'root')}><ChevronLeft />{browseMode === 'artist-songs' ? '返回歌星列表' : '返回'}</button>}
      {browseMode === 'singer-groups' && <div className="browse-options">{singerGroups.map(value => <button key={value} onClick={() => { setArtistGroup(value === '全部' ? '' : value); setBrowseMode('singers') }}>{value}</button>)}</div>}
      {browseMode === 'languages' && <div className="browse-options">{languages.map(value => <button key={value} onClick={() => chooseCategory('language', value)}>{value}</button>)}</div>}
      {browseMode === 'styles' && <div className="browse-options">{styles.map(value => <button key={value} onClick={() => chooseCategory('category', value)}>{value}</button>)}</div>}
      {browseMode === 'singers' && <><div className="browse-options" aria-label="歌手分类">{singerGroups.map(value => <button key={value} className={(value === '全部' ? artistGroup === '' : artistGroup === value) ? 'active' : ''} onClick={() => setArtistGroup(value === '全部' ? '' : value)}>{value}</button>)}</div><div className="artist-view-container" aria-label="歌手列表">{artistLoading ? <div className="catalog-message">正在加载歌星列表…</div> : artists.length === 0 ? <div className="catalog-message"><strong>该分类下暂无歌星数据</strong><p>请返回选择其他歌手分类，或先导入带歌手信息的曲库。</p></div> : <div className="artist-grid">{artists.map(artist => { const key = artist.id ?? artist.artistId ?? artist.name; const image = artist.imageUrl ?? artist.avatarUrl; return <button key={key} className="artist-card-btn" aria-label={artist.name} onClick={() => chooseArtist(artist)}><span className="artist-avatar-badge" style={{ backgroundColor: image ? 'transparent' : avatarColor(artist.name) }}>{image ? <img src={image} alt={artist.name} /> : artist.name.trim().slice(0, 1)}</span><strong className="artist-name-label">{artist.name}</strong><small className="artist-count-label">{artist.songCount} 首</small></button> })}</div>}</div></>}
    </section>

    {selectedArtist && <div className="active-artist"><span>歌手：<strong>{selectedArtist}</strong></span><button onClick={() => { setSelectedArtist(''); setSearchField('Any'); setBrowseMode('root') }}>查看全部</button></div>}
    {notice && <p className="catalog-notice" role="status">{notice}</p>}
    {error && <section className="catalog-message" role="alert"><p>{error}</p><button onClick={() => setRetry(current => current + 1)}>重试</button></section>}

    {!isArtistBrowse && <>
      {!error && loading && !result && <section className="catalog-message" aria-live="polite">正在搜索曲库…</section>}
      {!error && !loading && result?.items.length === 0 && <section className="catalog-message"><Music2 aria-hidden="true" /><strong>没有找到歌曲</strong><p>换个歌名、歌手、拼音或筛选条件试试。</p></section>}
      {!error && result && result.items.length > 0 && <section className="song-list" aria-label="搜索结果">{result.items.map(song => <article className="song-row" key={song.songId}><div><strong>{song.title}</strong><small>{song.artists}{song.language ? ` · ${song.language}` : ''}{song.quality ? ` · ${song.quality}` : ''}</small></div><span className="song-actions"><button className={favorites.has(song.songId) ? 'favorite active' : 'favorite'} aria-label={`${favorites.has(song.songId) ? '取消收藏' : '收藏'} ${song.title}`} onClick={() => void toggleFavorite(song)}><Heart aria-hidden="true" /></button><button className={animatedSongId === song.songId ? 'request-song queued' : 'request-song'} aria-label={`点播 ${song.title}`} onClick={() => void requestSong(song)} disabled={song.availability !== 'Available' || requestingSongId === song.songId}>{requestingSongId === song.songId ? '…' : animatedSongId === song.songId ? '✓' : '＋'}</button></span></article>)}<div className="catalog-pagination"><button onClick={() => setPage(current => Math.max(1, current - 1))} disabled={loading || page <= 1}>上一页</button><span>第 {page} / {totalPages} 页 · 共 {result.total} 首</span><button onClick={() => setPage(current => Math.min(totalPages, current + 1))} disabled={loading || page >= totalPages}>下一页</button></div></section>}
    </>}
  </>
}
