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
  return <section className="playback-page" aria-label="正在播放">
    <header className="playback-hero"><span className="album"><Music2 aria-hidden="true" /></span><div><small>{state?.state === 'Paused' ? '已暂停' : active ? '正在播放' : '播放器待机'}</small><strong>{label}</strong></div></header>
    <div className="transport-panel"><button aria-label={state?.state === 'Paused' ? '继续播放' : '暂停'} onClick={() => void command(state?.state === 'Paused' ? '/api/playback/play' : '/api/playback/pause')}>{state?.state === 'Paused' ? <Play /> : <Pause />}<span>{state?.state === 'Paused' ? '继续' : '暂停'}</span></button><button className="skip" aria-label="切歌" onClick={() => void command('/api/playback/skip')}><SkipForward /><span>切歌</span></button></div>
    <section className="volume-panel"><div><strong>音量</strong><span>{Math.round(state?.volume ?? 80)}%</span></div><input aria-label="音量" type="range" min="0" max="100" value={state?.volume ?? 80} onChange={event => setState(current => current ? { ...current, volume: Number(event.target.value) } : current)} onPointerUp={event => void command('/api/playback/volume', { volume: Number(event.currentTarget.value) })} /></section>
    {(audio.length > 0 || subtitles.length > 0) && <section className="track-panel">{audio.length > 0 && <label>原唱 / 伴奏<select aria-label="原唱伴奏" value={state?.audioTrackId ?? ''} onChange={event => void command('/api/playback/audio', { streamId: Number(event.target.value) })}>{audio.map(track => <option key={track.streamId} value={track.streamId}>{track.title || track.language || `音轨 ${track.streamId}`}</option>)}</select></label>}{subtitles.length > 0 && <label>字幕<select aria-label="字幕" value={state?.subtitleTrackId ?? ''} onChange={event => void command('/api/playback/subtitle', { streamId: event.target.value ? Number(event.target.value) : null })}><option value="">关闭字幕</option>{subtitles.map(track => <option key={track.streamId} value={track.streamId}>{track.title || track.language || `字幕 ${track.streamId}`}</option>)}</select></label>}</section>}
    {error && <p className="playback-error" role="alert">{error}</p>}
  </section>
}
