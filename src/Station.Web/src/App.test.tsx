import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { App } from './App'

describe('mobile application shell', () => {
  it('renders join route', () => { render(<MemoryRouter initialEntries={['/join']}><App /></MemoryRouter>); expect(screen.getByRole('heading', { name: 'AI-KTV Station' })).toBeInTheDocument(); expect(screen.getByRole('button', { name: '加入房间' })).toBeInTheDocument() })
  it('renders approved discovery shell and navigation', () => { render(<MemoryRouter initialEntries={['/room/discover']}><App /></MemoryRouter>); expect(screen.getByPlaceholderText('搜索歌名、歌手或拼音')).toBeInTheDocument(); expect(screen.getByText('夜曲 · 周杰伦')).toBeInTheDocument(); expect(screen.getByRole('navigation', { name: '主导航' })).toBeInTheDocument(); expect(screen.getByRole('button', { name: '点播 告白气球' })).toBeInTheDocument() })
})
