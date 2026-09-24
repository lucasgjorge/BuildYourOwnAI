import { Navigate } from 'react-router'
import { errorTitle } from '../../shared/api/client'
import { Alert } from '../../shared/ui'
import { useOrganizations } from './api'
import { CreateOrganizationForm } from './CreateOrganizationForm'

/** `/organizations`: opens the first organization's chat, or invites to create one. */
export function OrganizationsPage() {
  const organizations = useOrganizations()

  if (organizations.isPending) return <p className="p-8 text-muted">Carregando...</p>
  if (organizations.isError) return <div className="p-8"><Alert>{errorTitle(organizations.error)}</Alert></div>
  if (organizations.data.length > 0) return <Navigate to={`/organizations/${organizations.data[0].id}`} replace />

  return (
    <main className="mx-auto max-w-lg px-6 py-16">
      <h1 className="font-display text-3xl font-bold tracking-tight">Crie sua primeira organização</h1>
      <p className="mt-2 mb-8 text-muted">
        Uma organização guarda os documentos que as suas IAs usam para responder. Pode ser a sua empresa, um time ou um curso.
      </p>
      <CreateOrganizationForm />
    </main>
  )
}
