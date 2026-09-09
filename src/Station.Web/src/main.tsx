import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { App } from './App'
import { SessionProvider } from './state/session'
import './styles.css'
import './session.css'
import './features/catalog/catalog.css'
import './features/queue/queue.css'
import './features/queue/actions.css'
import './features/library/library.css'

createRoot(document.getElementById('root')!).render(<StrictMode><BrowserRouter><SessionProvider><App /></SessionProvider></BrowserRouter></StrictMode>)
