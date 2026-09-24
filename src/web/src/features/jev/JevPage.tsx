import { Thread } from '../chat/Thread'

/** `/jev`: the global Jev, routing among the AIs of all of the user's organizations. */
export function JevPage() {
  return (
    <div className="flex min-h-screen flex-col">
      <header className="border-b border-line bg-surface px-6 py-6 md:px-10">
        <h1 className="font-display text-2xl font-bold tracking-tight">Jev</h1>
        <p className="mt-1 text-sm text-muted">Pergunte sem escolher organização: o Jev procura entre todas as suas IAs.</p>
      </header>
      <Thread scope="global" />
    </div>
  )
}
