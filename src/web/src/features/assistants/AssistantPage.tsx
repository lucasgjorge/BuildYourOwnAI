import { Link, Navigate, useParams } from 'react-router'
import { errorTitle } from '../../shared/api/client'
import { Alert } from '../../shared/ui'
import { useAssistant } from './api'

/** `/assistants/:id`: an AI is talked to in its organization's chat, with it pinned. */
export function AssistantPage() {
  const { id = '' } = useParams()
  const assistant = useAssistant(id)

  if (assistant.isPending) return <p className="p-8 text-muted">Carregando...</p>
  if (assistant.isError)
    return (
      <main className="flex flex-col items-start gap-3 p-8">
        <Alert>{errorTitle(assistant.error)}</Alert>
        <Link to="/organizations" className="text-sm underline">Voltar</Link>
      </main>
    )

  return <Navigate to={`/organizations/${assistant.data.organizationId}?ia=${assistant.data.id}`} replace />
}
