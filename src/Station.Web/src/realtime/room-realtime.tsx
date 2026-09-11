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
  if (event.type === 'queue.added') return { ...state, version: event.version, queue: [...state.queue.filter(item => item.id !== (event.data as QueueEntry).id), event.data as QueueEntry].sort((a, b) => a.position - b.position) }
  if (event.type === 'queue.removed') return { ...state, version: event.version, queue: state.queue.filter(item => item.id !== (event.data as { itemId: string }).itemId) }
  if (event.type === 'queue.reordered') { const changed = event.data as QueueEntry; return { ...state, version: event.version, queue: state.queue.map(item => item.id === changed.id ? changed : item).sort((a, b) => a.position - b.position) } }
  if (event.type === 'queue.status') { const changed = event.data as { itemId: string; status: QueueEntry['status'] }; const terminal = ['Completed', 'Skipped', 'Failed'].includes(changed.status); return { ...state, version: event.version, queue: terminal ? state.queue.filter(item => item.id !== changed.itemId) : state.queue.map(item => item.id === changed.itemId ? { ...item, status: changed.status } : item) } }
  if (event.type === 'playback.changed') return { ...state, version: event.version, playback: event.data as PlaybackProgress }
  return { ...state, version: event.version }
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
