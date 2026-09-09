/// <reference types="node" />
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'

const read = (path: string) => readFileSync(resolve(process.cwd(), path), 'utf8')

describe('mobile compatibility baseline', () => {
  it('declares language, viewport fit and color scheme metadata', () => { const html = read('index.html'); expect(html).toContain('lang="zh-CN"'); expect(html).toContain('width=device-width, initial-scale=1, viewport-fit=cover'); expect(html).toContain('name="color-scheme"') })
  it('keeps 320px layout and accessible touch targets', () => { const base = read('src/styles.css'); const accessibility = read('src/accessibility.css'); expect(base).toContain('min-width:320px'); expect(accessibility).toContain('min-height: 44px'); expect(accessibility).toContain(':focus-visible'); expect(accessibility).toContain('safe-area-inset-bottom'); expect(accessibility).toContain('prefers-reduced-motion') })
  it('targets supported evergreen phone browsers', () => { const config = read('vite.config.ts'); expect(config).toContain("'chrome120'"); expect(config).toContain("'edge120'"); expect(config).toContain("'safari16.4'") })
})
