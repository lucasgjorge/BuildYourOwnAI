// Mirrors src/web/src/shared/lanes.ts.
import { colors } from './theme'

const lanes = [colors['lane-1'], colors['lane-2'], colors['lane-3'], colors['lane-4'], colors['lane-5'], colors['lane-6']]

/** The lane color of the AI at `index` in its organization. */
export const laneColor = (index: number) => lanes[Math.max(index, 0) % lanes.length]

/** Lane for an AI known only by name: stable for the same name. */
export function laneForName(name: string) {
  let hash = 0
  for (const char of name) hash = (hash * 31 + char.charCodeAt(0)) >>> 0
  return laneColor(hash)
}
