import { ListMusic, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { ApiError } from '../../api/client'
import { useRoomRealtime } from '../../realtime/room-realtime'
import { useSession } from '../../state/session'
import type { QueueEntry } from './types'

export function QueuePage() {
  const { api, session } = useSession()
  const { queue: items, connectionStatus } = useRoomRealtime()
  const [removed, setRemoved] = useState<string[]>([])
  const [error, setError] = useState('')
  async function remove(item: QueueEntry) { try { await api.delete(`/api/queue/${item.id}`); setRemoved(current => [...current, item.id]) } catch (value) { setError(value instanceof ApiError ? value.message : '无法删除这首歌曲。') } }
  const visible = items.filter(item => !removed.includes(item.id)); const mine = visible.filter(item => item.requestedByGuestId === session?.guestId)
  return <section className="queue-page"><header><h2>我的点歌</h2><p>我已点 {mine.length} 首 · 房间共 {visible.length} 首</p></header>{error && <div className="queue-message" role="alert"><p>{error}</p></div>}{!error && connectionStatus === 'connecting' && items.length === 0 && <div className="queue-message">正在同步队列…</div>}{!error && connectionStatus !== 'connecting' && mine.length === 0 && <div className="queue-message"><ListMusic aria-hidden="true" /><strong>还没有点歌</strong><p>从“点歌”页挑一首喜欢的歌吧。</p></div>}{!error && mine.map(item => <article className="queue-row" key={item.id}><span>{visible.indexOf(item) + 1}</span><div><strong>{item.title}</strong><small>{item.status === 'Playing' ? '正在播放' : item.status === 'Preparing' ? '正在准备' : '等待播放'}</small></div>{item.status === 'Waiting' && <button aria-label={`删除 ${item.title}`} onClick={() => void remove(item)}><Trash2 aria-hidden="true" /></button>}</article>)}</section>
}
