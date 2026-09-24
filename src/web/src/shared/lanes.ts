/** The lane color of the AI at `index` in its organization. */
export const laneColor = (index: number) => `var(--color-lane-${(Math.max(index, 0) % 6) + 1})`

/** Lane for an AI known only by name (all-assistants chat): stable for the same name. */
export function laneForName(name: string) {
  let hash = 0
  for (const char of name) hash = (hash * 31 + char.charCodeAt(0)) >>> 0
  return laneColor(hash)
}
