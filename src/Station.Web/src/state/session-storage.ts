import type { RoomSessionState } from './session'
const storageKey = 'ai-ktv-station.room-session.v1'
const profileKey = 'ai-ktv-station.household-profile.v1'
export function loadSession(now = Date.now()): RoomSessionState | null { const raw = sessionStorage.getItem(storageKey); if (!raw) return null; try { const value = JSON.parse(raw) as Partial<RoomSessionState>; if (!value.roomId || !value.guestId || !value.nickname || !value.token || !value.expiresAt || !value.role || Date.parse(value.expiresAt) <= now) { sessionStorage.removeItem(storageKey); return null } return value as RoomSessionState } catch { sessionStorage.removeItem(storageKey); return null } }
export function saveSession(value: RoomSessionState | null) { if (value) sessionStorage.setItem(storageKey, JSON.stringify(value)); else sessionStorage.removeItem(storageKey) }
export interface ProfileDeviceState { profileId: string; displayName: string; avatarUrl?: string; deviceToken: string }
export function loadProfile(): ProfileDeviceState | null { try { const value = JSON.parse(localStorage.getItem(profileKey) ?? '') as Partial<ProfileDeviceState>; return value.profileId && value.displayName && value.deviceToken ? value as ProfileDeviceState : null } catch { return null } }
export function saveProfile(value: ProfileDeviceState | null) { if (value) localStorage.setItem(profileKey, JSON.stringify(value)); else localStorage.removeItem(profileKey) }
