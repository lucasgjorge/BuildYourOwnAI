import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { errorTitle } from '../../shared/api/client'
import { Alert, Button, inputClass } from '../../shared/ui'
import { useCreateOrganization } from './api'

/** Creates an organization and opens its Base tab: without documents and AIs the chat has nothing to do yet. */
export function CreateOrganizationForm({ compact = false }: { compact?: boolean }) {
  const create = useCreateOrganization()
  const navigate = useNavigate()
  const [name, setName] = useState('')

  const submit = (event: FormEvent) => {
    event.preventDefault()
    create.mutate(name, {
      onSuccess: created => {
        setName('')
        navigate(`/organizations/${created.id}/knowledge`)
      },
    })
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-2">
      <label className={`flex flex-col gap-1 ${compact ? 'text-xs text-muted' : 'text-sm font-medium'}`}>
        Nome da organização
        <input value={name} onChange={e => setName(e.target.value)} placeholder="Ex.: Nexora Tech" className={inputClass} />
      </label>
      {create.isError && <Alert>{errorTitle(create.error)}</Alert>}
      <Button type="submit" disabled={create.isPending} className={compact ? 'self-stretch' : 'self-start'}>
        {create.isPending ? 'Processando...' : 'Criar organização'}
      </Button>
    </form>
  )
}
