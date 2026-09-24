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
})
