import { ListMusic, Trash2 } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import { ApiError } from '../../api/client'
import { useSession } from '../../state/session'
import type { QueueEntry } from './types'

export function QueuePage() {
  const { api, session } = useSession()
  const [items, setItems] = useState<QueueEntry[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const load = useCallback(async () => { setLoading(true); setError(''); try { setItems(await api.get<QueueEntry[]>('/api/queue')) } catch (value) { setError(value instanceof ApiError ? value.message : '队列暂时无法访问。') } finally { setLoading(false) } }, [api])
  useEffect(() => { void load() }, [load])
  async function remove(item: QueueEntry) { try { await api.delete(`/api/queue/${item.id}`); setItems(current => current.filter(entry => entry.id !== item.id)) } catch (value) { setError(value instanceof ApiError ? value.message : '无法删除这首歌曲。') } }
  const mine = items.filter(item => item.requestedByGuestId === session?.guestId)
  return <section className="queue-page"><header><h2>我的点歌</h2><p>我已点 {mine.length} 首 · 房间共 {items.length} 首</p></header>{error && <div className="queue-message" role="alert"><p>{error}</p><button onClick={() => void load()}>重试</button></div>}{!error && loading && <div className="queue-message">正在读取队列…</div>}{!error && !loading && mine.length === 0 && <div className="queue-message"><ListMusic aria-hidden="true" /><strong>还没有点歌</strong><p>从“点歌”页挑一首喜欢的歌吧。</p></div>}{!error && mine.map(item => <article className="queue-row" key={item.id}><span>{items.indexOf(item) + 1}</span><div><strong>{item.title}</strong><small>{item.status === 'Playing' ? '正在播放' : '等待播放'}</small></div>{item.status === 'Waiting' && <button aria-label={`删除 ${item.title}`} onClick={() => void remove(item)}><Trash2 aria-hidden="true" /></button>}</article>)}</section>
}
