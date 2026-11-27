/// <reference types="node" />
// The web is the guardrail of the app (AD-017). These tests keep that true for whoever adds the next screen.
import { readdirSync, readFileSync } from 'node:fs'
import { join, relative, resolve } from 'node:path'
import { colors } from '../shared/theme'

const root = process.cwd()
const src = resolve(root, 'src')

const files = (dir: string): string[] =>
  readdirSync(dir, { withFileTypes: true }).flatMap(entry =>
    entry.isDirectory() ? files(join(dir, entry.name)) : [join(dir, entry.name)])

// This file holds the patterns it looks for, so it does not scan itself.
const sources = files(src)
  .filter(file => /\.tsx?$/.test(file) && !file.endsWith('guardrails.test.ts'))
  .map(file => ({ name: relative(src, file).replaceAll('\\', '/'), text: readFileSync(file, 'utf8') }))

const offenders = (pattern: RegExp, allowed: string[] = []) =>
  sources.filter(f => !allowed.includes(f.name) && pattern.test(f.text)).map(f => f.name)

// C43
it('theme colors match the web', () => {
  const css = readFileSync(resolve(root, '../web/src/index.css'), 'utf8')
  const web = Object.fromEntries([...css.matchAll(/--color-([a-z0-9-]+):\s*(#[0-9a-fA-F]+)/g)].map(m => [m[1], m[2]]))

  expect(Object.keys(colors)).toHaveLength(17)
  expect(colors).toEqual(web)
})

// C44
it('no literal colors outside theme', () => {
  const literal = /#[0-9a-fA-F]{3,8}\b|\b(rgba?|hsla?)\(|[cC]olor['"]?\s*[:=]\s*\{?\s*['"](?!transparent)[a-zA-Z]+['"]/
  expect(offenders(literal, ['shared/theme.ts'])).toEqual([])
})

// C46
it('only the api client calls fetch', () => {
  expect(offenders(/(^|[^.\w])fetch\(/m)).toEqual(['shared/api/client.ts'])
})

// C6
it('only secure store persists data', () => {
  expect(offenders(/async-storage|expo-file-system/)).toEqual([])
})

// C54
it('no explicit any', () => {
  expect(offenders(/:\s*any\b|\bas any\b|<any[>,]/)).toEqual([])
})

// C55
it('routes only declare routes', () => {
  const routes = sources.filter(f => f.name.startsWith('app/'))
  expect(routes.length).toBeGreaterThan(0)
  expect(routes.filter(f => /useQuery|useMutation|fetch|StyleSheet/.test(f.text)).map(f => f.name)).toEqual([])
})

// C53
it('mobile mirrors types without importing web', () => {
  expect(offenders(/from ['"][^'"]*\/web\//)).toEqual([])
  const types = sources.filter(f => f.name.endsWith('/types.ts'))
  expect(types.length).toBeGreaterThan(0)
  expect(types.filter(f => !f.text.includes('// Mirrors')).map(f => f.name)).toEqual([])
})

// C52
it('app config pins expo sdk 58', () => {
  const pkg = JSON.parse(readFileSync(resolve(root, 'package.json'), 'utf8'))
  expect(pkg.dependencies.expo).toMatch(/^[~^]58\./)
  expect(pkg.main).toBe('expo-router/entry')
})
