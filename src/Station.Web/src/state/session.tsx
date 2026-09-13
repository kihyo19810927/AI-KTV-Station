import { createContext, type PropsWithChildren, useCallback, useContext, useMemo, useState } from 'react'
import { StationApiClient } from '../api/client'
import { loadProfile, loadSession, saveProfile, saveSession, type ProfileDeviceState } from './session-storage'

export type RoomRole = 'Guest' | 'Host'
export interface RoomSessionState { roomId: string; roomName: string; guestId: string; nickname: string; role: RoomRole; token: string; expiresAt: string }
interface SessionContextValue { session: RoomSessionState | null; setSession: (session: RoomSessionState | null) => void; profile: ProfileDeviceState | null; setProfile: (profile: ProfileDeviceState | null) => void; api: StationApiClient }
const SessionContext = createContext<SessionContextValue | null>(null)
export function SessionProvider({ children }: PropsWithChildren) { const [session, setValue] = useState<RoomSessionState | null>(() => loadSession()); const [profile, setProfileValue] = useState<ProfileDeviceState | null>(() => loadProfile()); const setSession = useCallback((next: RoomSessionState | null) => { saveSession(next); setValue(next) }, []); const setProfile = useCallback((next: ProfileDeviceState | null) => { saveProfile(next); setProfileValue(next) }, []); const api = useMemo(() => new StationApiClient(() => session?.token ?? null, '', () => profile?.deviceToken ?? null), [session?.token, profile?.deviceToken]); return <SessionContext.Provider value={{ session, setSession, profile, setProfile, api }}>{children}</SessionContext.Provider> }
export function useSession() { const value = useContext(SessionContext); if (!value) throw new Error('useSession must be used inside SessionProvider'); return value }
