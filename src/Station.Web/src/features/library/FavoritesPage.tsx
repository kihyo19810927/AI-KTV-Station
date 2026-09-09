import { Heart, Plus, Trash2 } from 'lucide-react'
import { useEffect, useState } from 'react'
import { ApiError } from '../../api/client'
import { useSession } from '../../state/session'

interface FavoriteSong { songId: string; title: string; artists: string; favoritedAt: string }

export function FavoritesPage() {
  const { api } = useSession(); const [items, setItems] = useState<FavoriteSong[]>([]); const [loading, setLoading] = useState(true); const [message, setMessage] = useState('')
  useEffect(() => { const controller = new AbortController(); api.get<FavoriteSong[]>('/api/library/favorites', controller.signal).then(setItems).catch(value => setMessage(value instanceof ApiError ? value.message : '收藏暂时无法访问。')).finally(() => setLoading(false)); return () => controller.abort() }, [api])
  async function remove(song: FavoriteSong) { try { await api.put(`/api/library/favorites/${song.songId}`, { favorite: false }); setItems(current => current.filter(item => item.songId !== song.songId)) } catch (value) { setMessage(value instanceof ApiError ? value.message : '无法取消收藏。') } }
  async function request(song: FavoriteSong) { try { await api.post('/api/queue', { songId: song.songId }); setMessage(`已点播《${song.title}》。`) } catch (value) { setMessage(value instanceof ApiError ? value.message : '点歌失败。') } }
  return <section className="favorites-page"><header><h2>我的收藏</h2><p>{items.length} 首喜欢的歌</p></header>{message && <p className="library-notice" role="status">{message}</p>}{loading && <div className="queue-message">正在读取收藏…</div>}{!loading && items.length === 0 && <div className="queue-message"><Heart aria-hidden="true" /><strong>还没有收藏</strong><p>在点歌页点击心形即可收藏。</p></div>}{items.map(song => <article className="library-row" key={song.songId}><div><strong>{song.title}</strong><small>{song.artists}</small></div><span><button aria-label={`点播 ${song.title}`} onClick={() => void request(song)}><Plus aria-hidden="true" /></button><button aria-label={`取消收藏 ${song.title}`} onClick={() => void remove(song)}><Trash2 aria-hidden="true" /></button></span></article>)}</section>
}
