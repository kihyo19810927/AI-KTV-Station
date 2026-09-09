import { Music2 } from 'lucide-react'
import { useRoomRealtime } from './room-realtime'

export function NowPlaying() { const { queue, playback } = useRoomRealtime(); const active = queue.find(item => item.status === 'Playing' || item.status === 'Preparing'); const label = active?.title ?? (playback?.state === 'Playing' ? '正在播放' : '等待主持人开始播放'); return <section className="now-playing" aria-label="正在播放"><span className="album"><Music2 aria-hidden="true" /></span><div><small>{playback?.state === 'Paused' ? '已暂停' : '正在播放'}</small><strong>{label}</strong></div><span>{active ? `#${queue.indexOf(active) + 1}` : '—'}</span></section> }
