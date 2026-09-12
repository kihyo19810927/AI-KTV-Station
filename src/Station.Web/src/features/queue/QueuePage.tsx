import { ListMusic, MoveUp, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { ApiError } from '../../api/client'
import { useRoomRealtime } from '../../realtime/room-realtime'
import { useSession } from '../../state/session'
import type { QueueEntry } from './types'

export function QueuePage() {
  const { api, session } = useSession()
  const { queue: items, connectionStatus, updateQueueItem } = useRoomRealtime()
  const [removed, setRemoved] = useState<string[]>([])
  const [error, setError] = useState('')
  const [pendingInsertId, setPendingInsertId] = useState('')
  async function remove(item: QueueEntry) { try { await api.delete(`/api/queue/${item.id}`); setRemoved(current => [...current, item.id]) } catch (value) { setError(value instanceof ApiError ? value.message : '无法删除这首歌曲。') } }
  async function insert(item: QueueEntry) { if (pendingInsertId) return; try { setError(''); setPendingInsertId(item.id); updateQueueItem(await api.post<QueueEntry>(`/api/queue/${item.id}/insert`, {})) } catch (value) { setError(value instanceof ApiError ? value.message : '无法插播这首歌曲。') } finally { setPendingInsertId('') } }
  const visible = items.filter(item => !removed.includes(item.id)); const mine = visible.filter(item => item.requestedByGuestId === session?.guestId)
  const statusText = (status: QueueEntry['status']) => ({ Probing: '正在探测媒体', ProbeFailed: '探测失败', Waiting: '等待播放', Preparing: '正在准备', Playing: '正在播放', Paused: '已暂停', Completed: '已播放', Skipped: '已跳过', Failed: '播放失败' })[status]
  return <section className="queue-page"><header><h2>我的点歌</h2><p>我已点 {mine.length} 首 · 房间共 {visible.length} 首</p></header>{error && <div className="queue-error" role="alert"><p>{error}</p></div>}{connectionStatus === 'connecting' && items.length === 0 && <div className="queue-message">正在同步队列…</div>}{connectionStatus !== 'connecting' && mine.length === 0 && <div className="queue-message"><ListMusic aria-hidden="true" /><strong>还没有点歌</strong><p>从“点歌”页挑一首喜欢的歌吧。</p></div>}{mine.map(item => <article className="queue-row" key={item.id}><span>{visible.indexOf(item) + 1}</span><div><strong>{item.title}</strong>{item.artists && <small>{item.artists}</small>}<small className={`queue-status ${item.status.toLowerCase()}`}>{statusText(item.status)}</small></div><div className="queue-actions">{['Probing', 'ProbeFailed', 'Waiting'].includes(item.status) && <button className="insert" aria-label={`插播 ${item.title}`} onClick={() => void insert(item)} disabled={pendingInsertId !== ''}><MoveUp aria-hidden="true" /></button>}{['Probing', 'ProbeFailed', 'Waiting'].includes(item.status) && <button aria-label={`删除 ${item.title}`} onClick={() => void remove(item)}><Trash2 aria-hidden="true" /></button>}</div></article>)}</section>
}
