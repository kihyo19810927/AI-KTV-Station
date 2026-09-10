import { Music2, Pause, Play, SkipForward } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useSession } from '../state/session'
import { useRoomRealtime } from './room-realtime'

interface Track { streamId: number; type: 'Audio' | 'Subtitle'; title?: string; language?: string }
interface PlayerState { state: string; volume: number; tracks: Track[]; audioTrackId?: number; subtitleTrackId?: number }

export function NowPlaying() {
  const { queue, playback } = useRoomRealtime(); const { api } = useSession()
  const [state, setState] = useState<PlayerState | null>(null); const [error, setError] = useState('')
  const active = queue.find(item => item.status === 'Playing' || item.status === 'Preparing')
  const label = active?.title ?? (playback?.state === 'Playing' ? '正在播放' : '等待播放')
  useEffect(() => { let disposed = false; const refresh = () => api.get<PlayerState>('/api/playback').then(value => { if (!disposed) setState(value) }).catch(() => undefined); void refresh(); const timer = window.setInterval(refresh, 2000); return () => { disposed = true; window.clearInterval(timer) } }, [api])
  const command = async (path: string, body?: unknown) => { setError(''); try { setState(await api.post<PlayerState>(path, body)) } catch { setError('控制失败，请稍后重试。') } }
  const tracks = Array.isArray(state?.tracks) ? state.tracks : []; const audio = tracks.filter(x => x.type === 'Audio'); const subtitles = tracks.filter(x => x.type === 'Subtitle')
  return <section className="now-playing" aria-label="正在播放"><span className="album"><Music2 aria-hidden="true" /></span><div className="playing-details"><small>{state?.state === 'Paused' ? '已暂停' : '正在播放'}</small><strong>{label}</strong><div className="mobile-controls"><button aria-label={state?.state === 'Paused' ? '继续播放' : '暂停'} onClick={() => void command(state?.state === 'Paused' ? '/api/playback/play' : '/api/playback/pause')}>{state?.state === 'Paused' ? <Play /> : <Pause />}</button><button aria-label="切歌" onClick={() => void command('/api/playback/skip')}><SkipForward /></button><label>音量<input aria-label="音量" type="range" min="0" max="100" value={state?.volume ?? 80} onChange={event => setState(current => current ? { ...current, volume: Number(event.target.value) } : current)} onPointerUp={event => void command('/api/playback/volume', { volume: Number(event.currentTarget.value) })} /></label></div>{audio.length > 0 && <select aria-label="原唱伴奏" value={state?.audioTrackId ?? ''} onChange={event => void command('/api/playback/audio', { streamId: Number(event.target.value) })}>{audio.map(track => <option key={track.streamId} value={track.streamId}>{track.title || track.language || `音轨 ${track.streamId}`}</option>)}</select>}{subtitles.length > 0 && <select aria-label="字幕" value={state?.subtitleTrackId ?? ''} onChange={event => void command('/api/playback/subtitle', { streamId: event.target.value ? Number(event.target.value) : null })}><option value="">关闭字幕</option>{subtitles.map(track => <option key={track.streamId} value={track.streamId}>{track.title || track.language || `字幕 ${track.streamId}`}</option>)}</select>}{error && <small role="alert">{error}</small>}</div><span>{active ? `#${queue.indexOf(active) + 1}` : '—'}</span></section>
}
