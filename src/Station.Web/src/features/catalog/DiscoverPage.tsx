import { Music2, Search } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { ApiError } from '../../api/client'
import { useSession } from '../../state/session'
import type { QueueEntry } from '../queue/types'

interface SongSearchItem { songId: string; title: string; artists: string; language?: string; category?: string; quality?: string; year?: number; availability: 'Available' | 'Offline' | 'Unreadable' }
interface SongSearchPage { items: SongSearchItem[]; total: number; page: number; pageSize: number }
interface Filters { language: string; category: string; quality: string; sort: string }
const initialFilters: Filters = { language: '', category: '', quality: '', sort: 'Relevance' }

function useDebouncedValue<T>(value: T, milliseconds: number) { const [debounced, setDebounced] = useState(value); useEffect(() => { const timer = window.setTimeout(() => setDebounced(value), milliseconds); return () => window.clearTimeout(timer) }, [value, milliseconds]); return debounced }

export function DiscoverPage() {
  const { api, session } = useSession()
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
  const queryKey = useMemo(() => JSON.stringify([debouncedText, filters]), [debouncedText, filters])

  useEffect(() => { setPage(1); setResult(null) }, [queryKey])
  useEffect(() => {
    const controller = new AbortController()
    const params = new URLSearchParams({ page: String(page), pageSize: '20', sort: filters.sort })
    if (debouncedText) params.set('text', debouncedText)
    if (filters.language) params.set('language', filters.language)
    if (filters.category) params.set('category', filters.category)
    if (filters.quality) params.set('quality', filters.quality)
    setLoading(true); setError('')
    api.get<SongSearchPage>(`/api/catalog/search?${params}`, controller.signal).then(next => setResult(current => page === 1 ? next : { ...next, items: [...(current?.items ?? []), ...next.items] })).catch(value => { if (!(value instanceof DOMException && value.name === 'AbortError')) setError(value instanceof ApiError ? value.message : '曲库暂时无法访问，请稍后重试。') }).finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [api, debouncedText, filters, page, retry])

  const updateFilter = (name: keyof Filters, value: string) => setFilters(current => ({ ...current, [name]: value }))
  async function requestSong(song: SongSearchItem) {
    setRequestingSongId(song.songId); setNotice('')
    try {
      const queue = await api.get<QueueEntry[]>('/api/queue')
      if (queue.some(item => item.songId === song.songId && item.requestedByGuestId === session?.guestId)) { setNotice(`《${song.title}》已经在你的队列中。`); return }
      await api.post<QueueEntry>('/api/queue', { songId: song.songId }); setNotice(`已点播《${song.title}》。`)
    } catch (value) {
      if (value instanceof ApiError && value.problem.code === 'queue.guest_limit_reached') setNotice('你的点歌数量已达到本房间上限。')
      else setNotice(value instanceof ApiError ? value.message : '点歌失败，请稍后重试。')
    } finally { setRequestingSongId('') }
  }
  const hasMore = result ? result.items.length < result.total : false
  return <>
    <label className="search-box"><Search aria-hidden="true" /><span className="sr-only">搜索歌曲</span><input value={text} onChange={event => setText(event.target.value)} placeholder="搜索歌名、歌手或拼音" /></label>
    <section className="now-playing" aria-label="正在播放"><span className="album"><Music2 aria-hidden="true" /></span><div><small>正在播放</small><strong>等待主持人开始播放</strong></div><span>—</span></section>
    <section className="catalog-filters" aria-label="曲库筛选">
      <label><span className="sr-only">语言</span><select value={filters.language} onChange={event => updateFilter('language', event.target.value)}><option value="">全部语言</option><option>国语</option><option>粤语</option><option>英语</option></select></label>
      <label><span className="sr-only">分类</span><select value={filters.category} onChange={event => updateFilter('category', event.target.value)}><option value="">全部分类</option><option>流行</option><option>经典</option><option>儿歌</option></select></label>
      <label><span className="sr-only">画质</span><select value={filters.quality} onChange={event => updateFilter('quality', event.target.value)}><option value="">全部画质</option><option>4K</option><option>1080P</option><option>720P</option></select></label>
      <label><span className="sr-only">排序</span><select value={filters.sort} onChange={event => updateFilter('sort', event.target.value)}><option value="Relevance">相关度</option><option value="Title">歌名</option><option value="YearDescending">年份</option></select></label>
    </section>
    {notice && <p className="catalog-notice" role="status">{notice}</p>}
    {error && <section className="catalog-message" role="alert"><p>{error}</p><button onClick={() => setRetry(current => current + 1)}>重试</button></section>}
    {!error && loading && !result && <section className="catalog-message" aria-live="polite">正在搜索曲库…</section>}
    {!error && !loading && result?.items.length === 0 && <section className="catalog-message"><Music2 aria-hidden="true" /><strong>没有找到歌曲</strong><p>换个歌名、歌手、拼音或筛选条件试试。</p></section>}
    {!error && result && result.items.length > 0 && <section className="song-list" aria-label="搜索结果">{result.items.map(song => <article className="song-row" key={song.songId}><div><strong>{song.title}</strong><small>{song.artists}{song.language ? ` · ${song.language}` : ''}{song.quality ? ` · ${song.quality}` : ''}</small></div><button aria-label={`点播 ${song.title}`} onClick={() => void requestSong(song)} disabled={song.availability !== 'Available' || requestingSongId === song.songId}>{requestingSongId === song.songId ? '…' : '＋'}</button></article>)}{hasMore && <button className="load-more" onClick={() => setPage(current => current + 1)} disabled={loading}>{loading ? '加载中…' : `继续加载（${result.items.length}/${result.total}）`}</button>}</section>}
  </>
}
