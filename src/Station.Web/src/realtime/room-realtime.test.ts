import { applyRoomEvent, type RoomRealtimeState } from './room-realtime'

const first = { id: 'one', songId: 'song-1', title: '第一首', requestedByGuestId: 'guest-1', requestedByNickname: '甲', position: 1024, status: 'Waiting' as const, requestedAt: '2026-09-10T00:00:00Z' }
const initial: RoomRealtimeState = { version: 2, queue: [first] }

describe('room realtime reducer', () => {
  it('applies queue events in version order and ignores duplicates', () => { const added = applyRoomEvent(initial, { version: 3, type: 'queue.added', data: { ...first, id: 'two', position: 2048 }, occurredAt: '' }); expect(added.queue).toHaveLength(2); expect(applyRoomEvent(added, { version: 3, type: 'queue.removed', data: { itemId: 'one' }, occurredAt: '' })).toBe(added); expect(applyRoomEvent(added, { version: 4, type: 'queue.removed', data: { itemId: 'one' }, occurredAt: '' }).queue.map(x => x.id)).toEqual(['two']) })
  it('reorders queue and updates playback without leaking protocol fields', () => { const withSecond = { ...initial, queue: [first, { ...first, id: 'two', position: 2048 }] }; const moved = applyRoomEvent(withSecond, { version: 3, type: 'queue.reordered', data: { ...first, id: 'two', position: 0 }, occurredAt: '' }); expect(moved.queue[0].id).toBe('two'); const playback = applyRoomEvent(moved, { version: 4, type: 'playback.changed', data: { playbackId: 'p1', state: 'Playing', position: '00:00:01' }, occurredAt: '' }); expect(playback.playback?.state).toBe('Playing') })
  it('updates probe status and removes terminal queue items immediately', () => { const ready = applyRoomEvent(initial, { version: 3, type: 'queue.status', data: { itemId: 'one', status: 'Waiting' }, occurredAt: '' }); expect(ready.queue[0].status).toBe('Waiting'); const completed = applyRoomEvent(ready, { version: 4, type: 'queue.status', data: { itemId: 'one', status: 'Completed' }, occurredAt: '' }); expect(completed.queue).toEqual([]) })
})
