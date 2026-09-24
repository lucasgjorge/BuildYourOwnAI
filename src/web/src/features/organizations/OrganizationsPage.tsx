import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { errorTitle } from '../../shared/api/client'
import { useCreateOrganization, useDeleteOrganization, useOrganizations } from './api'

export function OrganizationsPage() {
  const organizations = useOrganizations()
  const create = useCreateOrganization()
  const remove = useDeleteOrganization()
  const navigate = useNavigate()
  const [name, setName] = useState('')

  const submit = (event: FormEvent) => {
    event.preventDefault()
    create.mutate(name, { onSuccess: created => navigate(`/organizations/${created.id}`) })
  }

  const confirmDelete = (id: string, organizationName: string) => {
    if (window.confirm(`Apagar a organização "${organizationName}"? As IAs, os documentos e as lacunas dela serão apagados.`))
      remove.mutate(id)
  }

  return (
    <main className="mx-auto max-w-2xl p-6">
      <h1 className="mb-6 text-2xl font-semibold">Organizações</h1>

      {organizations.data?.length === 0 && <p className="mb-3 text-gray-600">Crie sua primeira organização</p>}
      <form onSubmit={submit} className="mb-8 flex flex-col gap-3 rounded border p-4">
        <label className="flex flex-col gap-1">
          Nome da organização
          <input value={name} onChange={e => setName(e.target.value)} className="rounded border p-2" />
        </label>
        {create.isError && <p role="alert" className="text-red-700">{errorTitle(create.error)}</p>}
        <button type="submit" disabled={create.isPending} className="self-start rounded bg-black px-4 py-2 text-white disabled:opacity-50">
          {create.isPending ? 'Processando...' : 'Criar organização'}
        </button>
      </form>

      {remove.isError && <p role="alert" className="mb-4 text-red-700">{errorTitle(remove.error)}</p>}
      {organizations.isPending && <p>Carregando...</p>}
      {organizations.isError && <p role="alert" className="text-red-700">{errorTitle(organizations.error)}</p>}
      <ul className="flex flex-col gap-2">
        {organizations.data?.map(o => (
          <li key={o.id} className="flex items-center justify-between rounded border p-3">
            <Link to={`/organizations/${o.id}`} className="font-medium underline">{o.name}</Link>
            <span className="flex items-center gap-4 text-sm text-gray-600">
              {o.assistantCount} IA(s) · {o.documentCount} documento(s)
              <button onClick={() => confirmDelete(o.id, o.name)} aria-label={`Apagar organização ${o.name}`} className="text-red-700">
                Apagar
              </button>
            </span>
          </li>
        ))}
      </ul>
    </main>
  )
}
