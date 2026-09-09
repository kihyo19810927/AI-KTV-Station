import type { RoomSessionState } from './session'
const storageKey = 'ai-ktv-station.room-session.v1'
export function loadSession(now = Date.now()): RoomSessionState | null { const raw = sessionStorage.getItem(storageKey); if (!raw) return null; try { const value = JSON.parse(raw) as Partial<RoomSessionState>; if (!value.roomId || !value.nickname || !value.token || !value.expiresAt || !value.role || Date.parse(value.expiresAt) <= now) { sessionStorage.removeItem(storageKey); return null } return value as RoomSessionState } catch { sessionStorage.removeItem(storageKey); return null } }
export function saveSession(value: RoomSessionState | null) { if (value) sessionStorage.setItem(storageKey, JSON.stringify(value)); else sessionStorage.removeItem(storageKey) }
