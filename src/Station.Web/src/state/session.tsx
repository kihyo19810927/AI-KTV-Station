import { createContext, type PropsWithChildren, useContext, useMemo, useState } from 'react'
import { StationApiClient } from '../api/client'

export type RoomRole = 'Guest' | 'Host'
export interface RoomSessionState { roomId: string; roomName: string; nickname: string; role: RoomRole; token: string; expiresAt: string }
interface SessionContextValue { session: RoomSessionState | null; setSession: (session: RoomSessionState | null) => void; api: StationApiClient }
const SessionContext = createContext<SessionContextValue | null>(null)
export function SessionProvider({ children }: PropsWithChildren) { const [session, setSession] = useState<RoomSessionState | null>(null); const api = useMemo(() => new StationApiClient(() => session?.token ?? null), [session?.token]); return <SessionContext.Provider value={{ session, setSession, api }}>{children}</SessionContext.Provider> }
export function useSession() { const value = useContext(SessionContext); if (!value) throw new Error('useSession must be used inside SessionProvider'); return value }
