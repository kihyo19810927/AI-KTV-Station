import { createContext, type PropsWithChildren, useCallback, useContext, useMemo, useState } from 'react'
import { StationApiClient } from '../api/client'
import { loadSession, saveSession } from './session-storage'

export type RoomRole = 'Guest' | 'Host'
export interface RoomSessionState { roomId: string; roomName: string; guestId: string; nickname: string; role: RoomRole; token: string; expiresAt: string }
interface SessionContextValue { session: RoomSessionState | null; setSession: (session: RoomSessionState | null) => void; api: StationApiClient }
const SessionContext = createContext<SessionContextValue | null>(null)
export function SessionProvider({ children }: PropsWithChildren) { const [session, setValue] = useState<RoomSessionState | null>(() => loadSession()); const setSession = useCallback((next: RoomSessionState | null) => { saveSession(next); setValue(next) }, []); const api = useMemo(() => new StationApiClient(() => session?.token ?? null), [session?.token]); return <SessionContext.Provider value={{ session, setSession, api }}>{children}</SessionContext.Provider> }
export function useSession() { const value = useContext(SessionContext); if (!value) throw new Error('useSession must be used inside SessionProvider'); return value }
