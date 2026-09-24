import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { errorTitle } from '../../shared/api/client'
import { useLogout } from '../auth/api'
import { useAssistants, useCreateAssistant, useDeleteAssistant } from './api'

export function AssistantsPage() {
  const assistants = useAssistants()
  const create = useCreateAssistant()
  const remove = useDeleteAssistant()
  const logout = useLogout()
  const navigate = useNavigate()
  const [name, setName] = useState('')
  const [instructions, setInstructions] = useState('')

  const submit = (event: FormEvent) => {
    event.preventDefault()
    create.mutate(
      { name, instructions: instructions.trim() === '' ? null : instructions },
      { onSuccess: () => { setName(''); setInstructions('') } },
    )
  }

  const confirmDelete = (id: string, assistantName: string) => {
    if (window.confirm(`Apagar a IA "${assistantName}" e todos os documentos dela?`)) remove.mutate(id)
  }

  return (
    <main className="mx-auto max-w-2xl p-6">
      <header className="mb-6 flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Minhas IAs</h1>
        <button onClick={() => logout.mutate(undefined, { onSuccess: () => navigate('/login') })} className="text-sm underline">
          Sair
        </button>
      </header>

      <form onSubmit={submit} className="mb-8 flex flex-col gap-3 rounded border p-4">
        <label className="flex flex-col gap-1">
          Nome
          <input value={name} onChange={e => setName(e.target.value)} className="rounded border p-2" />
        </label>
        <label className="flex flex-col gap-1">
          Instruções
          <textarea value={instructions} onChange={e => setInstructions(e.target.value)} rows={3} className="rounded border p-2" />
        </label>
        {create.isError && <p role="alert" className="text-red-700">{errorTitle(create.error)}</p>}
        <button type="submit" disabled={create.isPending} className="self-start rounded bg-black px-4 py-2 text-white disabled:opacity-50">
          {create.isPending ? 'Processando...' : 'Criar IA'}
        </button>
      </form>

      {remove.isError && <p role="alert" className="mb-4 text-red-700">{errorTitle(remove.error)}</p>}
      {assistants.isPending && <p>Carregando...</p>}
      {assistants.isError && <p role="alert" className="text-red-700">{errorTitle(assistants.error)}</p>}
      {assistants.data?.length === 0 && <p className="text-gray-600">Você ainda não criou nenhuma IA</p>}
      <ul className="flex flex-col gap-2">
        {assistants.data?.map(a => (
          <li key={a.id} className="flex items-center justify-between rounded border p-3">
            <Link to={`/assistants/${a.id}`} className="font-medium underline">{a.name}</Link>
            <span className="flex items-center gap-4 text-sm text-gray-600">
              {a.documentCount} documento(s)
              <button onClick={() => confirmDelete(a.id, a.name)} aria-label={`Apagar IA ${a.name}`} className="text-red-700">
                Apagar
              </button>
            </span>
          </li>
        ))}
      </ul>
    </main>
  )
}
