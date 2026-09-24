import { Link, useSearchParams } from 'react-router'
import { useCurrentOrganization } from '../organizations/context'
import { Thread } from './Thread'

/** `/organizations/:id` (Conversa tab): the organization's chat, where Jev routes each message. */
export function ChatPage() {
  const organization = useCurrentOrganization()
  const [params] = useSearchParams()

  if (organization.assistants.length === 0)
    return (
      <main className="mx-auto max-w-lg px-6 py-16 text-center">
        <p className="font-display text-xl font-bold">Esta organização ainda não tem IAs</p>
        <p className="mt-2 text-muted">Suba os documentos e crie as IAs na Base. Depois é só perguntar aqui.</p>
        <Link to={`/organizations/${organization.id}/knowledge`} className="mt-6 inline-block rounded-md bg-ink px-4 py-2 text-sm font-medium text-surface">
          Abrir a Base
        </Link>
      </main>
    )

  // Keyed by organization: switching organizations starts an empty thread (door 2).
  return (
    <Thread
      key={organization.id}
      scope="organization"
      organizationId={organization.id}
      organizationName={organization.name}
      assistants={organization.assistants}
      pinnedId={params.get('ia') ?? undefined}
    />
  )
}
