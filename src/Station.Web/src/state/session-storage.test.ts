import { beforeEach, describe, expect, it } from 'vitest'
import { loadSession, saveSession } from './session-storage'

const key = 'ai-ktv-station.room-session.v1'
const session = { roomId: 'room-1', roomName: '客厅 KTV', guestId: 'guest-1', nickname: '访客', role: 'Guest' as const, token: 'temporary-token', expiresAt: '2099-01-01T00:00:00Z' }

describe('room session storage', () => {
  beforeEach(() => { sessionStorage.clear(); localStorage.clear() })
  it('round trips a valid session in session storage only', () => { saveSession(session); expect(loadSession()).toEqual(session); expect(localStorage.length).toBe(0) })
  it('removes expired sessions', () => { sessionStorage.setItem(key, JSON.stringify({ ...session, expiresAt: '2020-01-01T00:00:00Z' })); expect(loadSession()).toBeNull(); expect(sessionStorage.getItem(key)).toBeNull() })
  it('removes malformed sessions', () => { sessionStorage.setItem(key, '{broken'); expect(loadSession()).toBeNull(); expect(sessionStorage.getItem(key)).toBeNull() })
})
