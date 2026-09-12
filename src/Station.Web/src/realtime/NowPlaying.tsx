import { Music2, Pause, Play, SkipForward } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useSession } from '../state/session'
import { useRoomRealtime } from './room-realtime'

interface Track { streamId: number; type: 'Audio' | 'Subtitle'; title?: string; language?: string }
interface PlayerState { state: string; volume: number; tracks: Track[]; position?: string | number; duration?: string | number; audioTrackId?: number; subtitleTrackId?: number }

function seconds(value: string | number | undefined) {
  if (typeof value === 'number' && Number.isFinite(value)) return Math.max(0, value)
  if (typeof value !== 'string') return 0
  const parts = value.split(':').map(Number)
  if (parts.some(part => !Number.isFinite(part))) return 0
  return parts.length === 3 ? parts[0] * 3600 + parts[1] * 60 + parts[2] : parts.length === 2 ? parts[0] * 60 + parts[1] : Number(parts[0]) || 0
}

function trackName(track: Track) {
  return track.title?.trim() || track.language?.trim() || (track.type === 'Audio' ? `音轨 ${track.streamId}` : `字幕 ${track.streamId}`)
}

export function NowPlaying() {
  const { queue, playback } = useRoomRealtime(); const { api } = useSession()
  const [state, setState] = useState<PlayerState | null>(null); const [error, setError] = useState('')
  const [volumeDraft, setVolumeDraft] = useState(80); const [volumeDragging, setVolumeDragging] = useState(false)
  const [positionDraft, setPositionDraft] = useState(0); const [positionDragging, setPositionDragging] = useState(false)
  const volumeTimer = useRef<number | undefined>(undefined)
  const active = queue.find(item => item.status === 'Playing' || item.status === 'Preparing')
  const playbackState = playback?.state ?? state?.state
  useEffect(() => { let disposed = false; const refresh = () => api.get<PlayerState>('/api/playback').then(value => { if (!disposed) setState(value) }).catch(() => undefined); void refresh(); const timer = window.setInterval(refresh, 2000); return () => { disposed = true; window.clearInterval(timer); if (volumeTimer.current) window.clearTimeout(volumeTimer.current) } }, [api])
  useEffect(() => { if (state && !volumeDragging) setVolumeDraft(state.volume) }, [state?.volume, volumeDragging, state])
  useEffect(() => { if (state && !positionDragging) setPositionDraft(seconds(state.position)) }, [state?.position, positionDragging, state])
  const command = async (path: string, body?: unknown) => { setError(''); try { setState(await api.post<PlayerState>(path, body)) } catch { setError('控制失败，请稍后重试。') } }
  const tracks = Array.isArray(state?.tracks) ? state.tracks : []; const audio = tracks.filter(x => x.type === 'Audio'); const subtitles = tracks.filter(x => x.type === 'Subtitle')
  const duration = Math.max(1, seconds(state?.duration))
  const displayArtists = active?.artists?.trim()
  const displayTitle = active?.title ?? (playbackState === 'Playing' || playbackState === 'Paused' ? '正在播放' : '等待播放')
  const updateVolume = (value: number) => {
    setVolumeDraft(value)
    setState(current => current ? { ...current, volume: value } : current)
    if (volumeTimer.current) window.clearTimeout(volumeTimer.current)
    volumeTimer.current = window.setTimeout(() => void command('/api/playback/volume', { volume: value }), 160)
  }
  const updatePosition = (value: number) => { setPositionDraft(value); setState(current => current ? { ...current, position: value } : current) }
  const commitPosition = () => { setPositionDragging(false); void command('/api/playback/seek', { positionSeconds: positionDraft }) }
  return <section className="playback-page" aria-label="正在播放">
    <header className="playback-hero"><span className="album"><Music2 aria-hidden="true" /></span><div><small>{playbackState === 'Paused' ? '已暂停' : active || playbackState === 'Playing' ? '正在播放' : '播放器待机'}</small><strong>{displayTitle}</strong>{displayArtists && <span className="playback-artist">{displayArtists}</span>}</div></header>
    <section className="position-panel"><div><span>{positionDraft.toFixed(0)} 秒</span><span>{duration.toFixed(0)} 秒</span></div><input aria-label="播放进度" type="range" min="0" max={duration} step="1" value={Math.min(positionDraft, duration)} onPointerDown={() => setPositionDragging(true)} onChange={event => updatePosition(Number(event.target.value))} onPointerUp={commitPosition} /></section>
    <div className="transport-panel"><button aria-label={playbackState === 'Paused' ? '继续播放' : '暂停'} onClick={() => void command(playbackState === 'Paused' ? '/api/playback/play' : '/api/playback/pause')}>{playbackState === 'Paused' ? <Play /> : <Pause />}<span>{playbackState === 'Paused' ? '继续' : '暂停'}</span></button><button className="skip" aria-label="切歌" onClick={() => void command('/api/playback/skip')}><SkipForward /><span>切歌</span></button></div>
    <section className="volume-panel"><div><strong>音量</strong><span>{Math.round(volumeDraft)}%</span></div><input aria-label="音量" type="range" min="0" max="100" value={volumeDraft} onPointerDown={() => setVolumeDragging(true)} onChange={event => updateVolume(Number(event.target.value))} onPointerUp={() => setVolumeDragging(false)} /></section>
    {(audio.length > 0 || subtitles.length > 0) && <section className="track-panel">{audio.length > 0 && <label>原唱 / 伴奏<select aria-label="原唱伴奏" value={state?.audioTrackId ?? ''} onChange={event => void command('/api/playback/audio', { streamId: Number(event.target.value) })}>{audio.map(track => <option key={track.streamId} value={track.streamId}>{trackName(track)}</option>)}</select></label>}{subtitles.length > 0 && <label>字幕<select aria-label="字幕" value={state?.subtitleTrackId ?? ''} onChange={event => void command('/api/playback/subtitle', { streamId: event.target.value ? Number(event.target.value) : null })}><option value="">关闭字幕</option>{subtitles.map(track => <option key={track.streamId} value={track.streamId}>{trackName(track)}</option>)}</select></label>}</section>}
    {error && <p className="playback-error" role="alert">{error}</p>}
  </section>
}
