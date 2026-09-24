import { Thread } from '../chat/Thread'

/** `/all`: automatic choice among the AIs of all of the user's organizations. */
export function AllAssistantsPage() {
  return (
    <div className="flex min-h-screen flex-col">
      <header className="border-b border-line bg-surface px-6 py-6 md:px-10">
        <h1 className="font-display text-2xl font-bold tracking-tight">Todas as IAs</h1>
        <p className="mt-1 text-sm text-muted">Pergunte sem escolher organização: a pergunta vai para a IA mais indicada entre todas as suas.</p>
      </header>
      <Thread scope="global" />
    </div>
  )
}
