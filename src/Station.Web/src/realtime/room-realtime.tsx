import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr'
import { createContext, type PropsWithChildren, useContext, useEffect, useRef, useState } from 'react'
import { useSession } from '../state/session'
import type { QueueEntry } from '../features/queue/types'

export type ConnectionStatus = 'connecting' | 'connected' | 'reconnecting' | 'offline'
export interface PlaybackProgress { playbackId?: string; state: string; position: string; duration?: string }
export interface RoomRealtimeEvent { version: number; type: string; data: unknown; occurredAt: string }
export interface RoomRealtimeSnapshot { version: number; roomId: string; queue: QueueEntry[]; playback?: PlaybackProgress }
interface RoomRealtimeSync { version: number; snapshot?: RoomRealtimeSnapshot; events: RoomRealtimeEvent[] }
export interface RoomRealtimeState { version: number; queue: QueueEntry[]; playback?: PlaybackProgress }
interface RoomRealtimeContextValue extends RoomRealtimeState { connectionStatus: ConnectionStatus; addQueueItem: (item: QueueEntry) => void; updateQueueItem: (item: QueueEntry) => void }
const RoomRealtimeContext = createContext<RoomRealtimeContextValue | null>(null)
const emptyState: RoomRealtimeState = { version: 0, queue: [] }

export function applyRoomEvent(state: RoomRealtimeState, event: RoomRealtimeEvent): RoomRealtimeState {
  if (event.version <= state.version) return state
  if (event.type === 'queue.added') { const item = normalizeQueueEntry(event.data); return item ? { ...state, version: event.version, queue: [...state.queue.filter(existing => existing.id !== item.id), item].sort((a, b) => a.position - b.position) } : { ...state, version: event.version } }
  if (event.type === 'queue.removed') { const itemId = readString(event.data, 'itemId'); return { ...state, version: event.version, queue: itemId ? state.queue.filter(item => item.id !== itemId) : state.queue } }
  if (event.type === 'queue.reordered') { const changed = normalizeQueueEntry(event.data); return changed ? { ...state, version: event.version, queue: state.queue.map(item => item.id === changed.id ? changed : item).sort((a, b) => a.position - b.position) } : { ...state, version: event.version } }
  if (event.type === 'queue.status') { const itemId = readString(event.data, 'itemId'); const status = readString(event.data, 'status') as QueueEntry['status'] | undefined; const terminal = status !== undefined && ['Completed', 'Skipped', 'Failed'].includes(status); return { ...state, version: event.version, queue: itemId && status ? terminal ? state.queue.filter(item => item.id !== itemId) : state.queue.map(item => item.id === itemId ? { ...item, status } : item) : state.queue } }
  if (event.type === 'playback.changed') return { ...state, version: event.version, playback: event.data as PlaybackProgress }
  return { ...state, version: event.version }
}

function readValue(data: unknown, name: string): unknown {
  if (!data || typeof data !== 'object') return undefined
  const record = data as Record<string, unknown>
  return record[name] ?? record[`${name[0].toUpperCase()}${name.slice(1)}`]
}

function readString(data: unknown, name: string): string | undefined {
  const value = readValue(data, name)
  return typeof value === 'string' ? value : undefined
}

function normalizeQueueEntry(data: unknown): QueueEntry | undefined {
  if (!data || typeof data !== 'object') return undefined
  const item = data as Record<string, unknown>
  const id = readString(data, 'id')
  const songId = readString(data, 'songId')
  const title = readString(data, 'title')
  const requestedByGuestId = readString(data, 'requestedByGuestId')
  const requestedByNickname = readString(data, 'requestedByNickname')
  const status = readString(data, 'status') as QueueEntry['status'] | undefined
  const position = readValue(data, 'position')
  const requestedAt = readString(data, 'requestedAt')
  if (!id || !songId || !title || !requestedByGuestId || !requestedByNickname || !status || typeof position !== 'number' || !requestedAt) return undefined
  return { id, songId, title, requestedByGuestId, requestedByNickname, position, status, requestedAt }
}

export function RoomRealtimeProvider({ children }: PropsWithChildren) {
  const { session } = useSession()
  const [state, setState] = useState(emptyState)
  const [connectionStatus, setConnectionStatus] = useState<ConnectionStatus>('connecting')
  const version = useRef(0)
  const addQueueItem = (item: QueueEntry) => setState(current => ({ ...current, queue: [...current.queue.filter(existing => existing.id !== item.id), item].sort((a, b) => a.position - b.position) }))
  const updateQueueItem = addQueueItem
  useEffect(() => {
    if (!session) return
    let disposed = false
    let connection: HubConnection
    const accept = (event: RoomRealtimeEvent) => setState(current => { const next = applyRoomEvent(current, event); version.current = next.version; return next })
    const subscribe = async () => { const sync = await connection.invoke<RoomRealtimeSync>('Subscribe', session.token, version.current || null); if (disposed) return; setState(current => { let next = sync.snapshot ? { version: sync.snapshot.version, queue: sync.snapshot.queue, playback: sync.snapshot.playback } : current; for (const event of sync.events) next = applyRoomEvent(next, event); version.current = Math.max(sync.version, next.version); return { ...next, version: version.current } }); setConnectionStatus('connected') }
    connection = new HubConnectionBuilder().withUrl('/hubs/room').withAutomaticReconnect([0, 1000, 3000, 10000]).configureLogging(LogLevel.Warning).build()
    connection.on('roomEvent', accept)
    connection.onreconnecting(() => setConnectionStatus('reconnecting'))
    connection.onreconnected(() => { setConnectionStatus('connecting'); void subscribe().catch(() => setConnectionStatus('offline')) })
    connection.onclose(() => { if (!disposed) setConnectionStatus('offline') })
    setConnectionStatus('connecting')
    void connection.start().then(subscribe).catch(() => { if (!disposed) setConnectionStatus('offline') })
    return () => { disposed = true; connection.off('roomEvent', accept); if (connection.state !== HubConnectionState.Disconnected) void connection.stop() }
  }, [session])
  return <RoomRealtimeContext.Provider value={{ ...state, connectionStatus, addQueueItem, updateQueueItem }}>{children}</RoomRealtimeContext.Provider>
}

export function useRoomRealtime() { const value = useContext(RoomRealtimeContext); if (!value) throw new Error('useRoomRealtime must be used inside RoomRealtimeProvider'); return value }
