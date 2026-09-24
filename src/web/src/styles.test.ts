/// <reference types="node" />
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

// Read from disk (vitest runs from src/web): the test runner stubs CSS imports.
const css = readFileSync(resolve(process.cwd(), 'src/index.css'), 'utf8')

describe('global styles', () => {
  // C29
  it('styles keep focus visible and respect reduced motion', () => {
    expect(css).toMatch(/:focus-visible\s*\{[^}]*outline:\s*2px solid/)
    const reduced = css.match(/@media \(prefers-reduced-motion: reduce\)\s*\{([\s\S]*?)\n\}/)
    expect(reduced).not.toBeNull()
    expect(reduced![1]).toMatch(/animation:\s*none !important/)
    expect(reduced![1]).toMatch(/transition:\s*none !important/)
  })

  // Lane colors are read only from inline styles; inside @theme, Tailwind would drop them from the build.
  it('lane colors are declared outside the tailwind theme', () => {
    const root = css.match(/:root\s*\{([^}]*)\}/)
    expect(root).not.toBeNull()
    for (let lane = 1; lane <= 6; lane++) expect(root![1]).toMatch(new RegExp(`--color-lane-${lane}:\s*#`))
    const theme = css.match(/@theme\s*\{([^}]*)\}/)![1]
    expect(theme).not.toMatch(/--color-lane-/)
  })
})
