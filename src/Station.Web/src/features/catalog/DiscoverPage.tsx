import { ChevronLeft, Heart, Languages, Music2, Search, Shapes, Users } from 'lucide-react'
import { useEffect, useMemo, useRef, useState } from 'react'
import { ApiError } from '../../api/client'
import { useSession } from '../../state/session'
import type { QueueEntry } from '../queue/types'
import { useRoomRealtime } from '../../realtime/room-realtime'

interface SongSearchItem { songId: string; title: string; artists: string; language?: string; category?: string; quality?: string; year?: number; availability: 'Available' | 'Offline' | 'Unreadable' }
interface SongSearchPage { items: SongSearchItem[]; total: number; page: number; pageSize: number }
interface Filters { artistGroup: string; language: string; category: string; sort: string }
interface FavoriteSong { songId: string }
interface ArtistItem { artistId: string; name: string; songCount: number; imageUrl?: string }
type BrowseMode = 'root' | 'singer-groups' | 'singers' | 'languages' | 'styles'
const initialFilters: Filters = { artistGroup: '', language: '', category: '', sort: 'Relevance' }
const singerGroups = ['全部', '华语男歌手', '华语女歌手', '华语组合', '欧美歌手', '日韩歌手', '其他']
const languages = ['全部', '国语', '粤语', '台语', '闽南语', '英语', '日语', '韩语', '纯音乐']
const styles = ['全部', '流行', '经典', '摇滚', '民谣', '儿歌', '舞曲', '影视原声', '纯音乐']

function useDebouncedValue<T>(value: T, milliseconds: number) { const [debounced, setDebounced] = useState(value); useEffect(() => { const timer = window.setTimeout(() => setDebounced(value), milliseconds); return () => window.clearTimeout(timer) }, [value, milliseconds]); return debounced }

export function DiscoverPage() {
  const { api, session } = useSession()
  const { addQueueItem } = useRoomRealtime()
  const [text, setText] = useState('')
  const debouncedText = useDebouncedValue(text.trim(), 300)
  const [filters, setFilters] = useState(initialFilters)
  const [page, setPage] = useState(1)
  const [result, setResult] = useState<SongSearchPage | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [retry, setRetry] = useState(0)
  const [requestingSongId, setRequestingSongId] = useState('')
  const [notice, setNotice] = useState('')
  const [favorites, setFavorites] = useState<Set<string>>(new Set())
  const [browseMode, setBrowseMode] = useState<BrowseMode>('root')
  const [artists, setArtists] = useState<ArtistItem[]>([])
  const [artistGroup, setArtistGroup] = useState('')
  const requestSequence = useRef(0)
  const queryKey = useMemo(() => JSON.stringify([debouncedText, filters]), [debouncedText, filters])

  useEffect(() => { const controller = new AbortController(); api.get<FavoriteSong[]>('/api/library/favorites', controller.signal).then(items => setFavorites(new Set(items.map(item => item.songId)))).catch(() => undefined); return () => controller.abort() }, [api])
  useEffect(() => { if (browseMode !== 'singers') return; const controller = new AbortController(); const params = artistGroup ? `?artistGroup=${encodeURIComponent(artistGroup)}` : ''; api.get<ArtistItem[]>(`/api/catalog/artists${params}`, controller.signal).then(setArtists).catch(() => setArtists([])); return () => controller.abort() }, [api, artistGroup, browseMode])

  useEffect(() => { setPage(1); setResult(null) }, [queryKey])
  useEffect(() => {
    const controller = new AbortController()
    const sequence = ++requestSequence.current
    const params = new URLSearchParams({ page: String(page), pageSize: '20', sort: filters.sort })
    if (debouncedText) params.set('text', debouncedText)
    if (filters.language) params.set('language', filters.language)
    if (filters.category) params.set('category', filters.category)
    if (filters.artistGroup) params.set('artistGroup', filters.artistGroup)
    setLoading(true); setError('')
    const request = api.get<SongSearchPage>(`/api/catalog/search?${params}`, controller.signal)
    request.then(next => { if (sequence === requestSequence.current) setResult(current => page === 1 ? next : { ...next, items: [...(current?.items ?? []), ...next.items] }) }).catch(value => { if (sequence === requestSequence.current && !(value instanceof DOMException && value.name === 'AbortError')) setError(value instanceof ApiError ? value.message : '曲库暂时无法访问，请稍后重试。') }).finally(() => { if (sequence === requestSequence.current && !controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [api, debouncedText, filters, page, retry])

  const chooseCategory = (name: 'language' | 'category', value: string) => { setFilters({ ...initialFilters, [name]: value === '全部' ? '' : value }); setBrowseMode('root') }
  async function requestSong(song: SongSearchItem) {
    setRequestingSongId(song.songId); setNotice('')
    try {
      const queue = await api.get<QueueEntry[]>('/api/queue')
      if (queue.some(item => item.songId === song.songId && item.requestedByGuestId === session?.guestId)) { setNotice(`《${song.title}》已经在你的队列中。`); return }
      const queued = await api.post<QueueEntry>('/api/queue', { songId: song.songId }); addQueueItem(queued); setNotice(`已点播《${song.title}》。`)
    } catch (value) {
      if (value instanceof ApiError && value.problem.code === 'queue.guest_limit_reached') setNotice('你的点歌数量已达到本房间上限。')
      else setNotice(value instanceof ApiError ? value.message : '点歌失败，请稍后重试。')
    } finally { setRequestingSongId('') }
  }
  async function toggleFavorite(song: SongSearchItem) { const favorite = !favorites.has(song.songId); try { await api.put(`/api/library/favorites/${song.songId}`, { favorite }); setFavorites(current => { const next = new Set(current); if (favorite) next.add(song.songId); else next.delete(song.songId); return next }); setNotice(favorite ? `已收藏《${song.title}》。` : `已取消收藏《${song.title}》。`) } catch (value) { setNotice(value instanceof ApiError ? value.message : '收藏操作失败。') } }
  const hasMore = result ? result.items.length < result.total : false
  return <>
    <label className="search-box"><Search aria-hidden="true" /><span className="sr-only">搜索歌曲</span><input value={text} onChange={event => setText(event.target.value)} placeholder="搜索歌名、歌手或拼音" /></label>
    <section className="browse-menu" aria-label="曲库分类">
      {browseMode === 'root' && <div className="browse-root"><button onClick={() => setBrowseMode('singer-groups')}><Users />按歌星</button><button onClick={() => setBrowseMode('languages')}><Languages />按语种</button><button onClick={() => setBrowseMode('styles')}><Shapes />按风格</button></div>}
      {browseMode !== 'root' && <button className="browse-back" onClick={() => setBrowseMode(browseMode === 'singers' ? 'singer-groups' : 'root')}><ChevronLeft />返回</button>}
      {browseMode === 'singer-groups' && <div className="browse-options">{singerGroups.map(value => <button key={value} onClick={() => { setArtistGroup(value === '全部' ? '' : value); setBrowseMode('singers') }}>{value}</button>)}</div>}
      {browseMode === 'languages' && <div className="browse-options">{languages.map(value => <button key={value} onClick={() => chooseCategory('language', value)}>{value}</button>)}</div>}
      {browseMode === 'styles' && <div className="browse-options">{styles.map(value => <button key={value} onClick={() => chooseCategory('category', value)}>{value}</button>)}</div>}
      {browseMode === 'singers' && <div className="artist-grid">{artists.map(artist => <button key={artist.artistId} onClick={() => { setText(artist.name); setFilters(initialFilters); setBrowseMode('root') }}><span>{artist.imageUrl ? <img src={artist.imageUrl} alt="" /> : artist.name.slice(0, 1)}</span><strong>{artist.name}</strong></button>)}</div>}
    </section>
    {notice && <p className="catalog-notice" role="status">{notice}</p>}
    {error && <section className="catalog-message" role="alert"><p>{error}</p><button onClick={() => setRetry(current => current + 1)}>重试</button></section>}
    {!error && loading && !result && <section className="catalog-message" aria-live="polite">正在搜索曲库…</section>}
    {!error && !loading && result?.items.length === 0 && <section className="catalog-message"><Music2 aria-hidden="true" /><strong>没有找到歌曲</strong><p>换个歌名、歌手、拼音或筛选条件试试。</p></section>}
    {!error && result && result.items.length > 0 && <section className="song-list" aria-label="搜索结果">{result.items.map(song => <article className="song-row" key={song.songId}><div><strong>{song.title}</strong><small>{song.artists}{song.language ? ` · ${song.language}` : ''}{song.quality ? ` · ${song.quality}` : ''}</small></div><span className="song-actions"><button className={favorites.has(song.songId) ? 'favorite active' : 'favorite'} aria-label={`${favorites.has(song.songId) ? '取消收藏' : '收藏'} ${song.title}`} onClick={() => void toggleFavorite(song)}><Heart aria-hidden="true" /></button><button aria-label={`点播 ${song.title}`} onClick={() => void requestSong(song)} disabled={song.availability !== 'Available' || requestingSongId === song.songId}>{requestingSongId === song.songId ? '…' : '＋'}</button></span></article>)}{hasMore && <button className="load-more" onClick={() => setPage(current => current + 1)} disabled={loading}>{loading ? '加载中…' : `继续加载（${result.items.length}/${result.total}）`}</button>}</section>}
  </>
}
