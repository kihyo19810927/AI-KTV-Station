import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  build: { outDir: '../Station.Server/wwwroot', emptyOutDir: true, target: ['chrome120', 'edge120', 'safari16.4'] },
  server: { proxy: { '/api': 'http://127.0.0.1:5090', '/hubs': { target: 'http://127.0.0.1:5090', ws: true } } },
  test: { environment: 'jsdom', setupFiles: './src/test/setup.ts', globals: true },
})
