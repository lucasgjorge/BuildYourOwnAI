import { useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import { errorTitle } from '../../shared/api/client'
import { useCreateAssistant, useDeleteAssistant } from '../assistants/api'
import { useDeleteDocument, useDocuments, useOrganization, useUploadDocument } from './api'
import type { OrganizationAssistant } from './types'

export function OrganizationPage() {
  const { id = '' } = useParams()
  const organization = useOrganization(id)

  if (organization.isPending) return <p className="p-6">Carregando...</p>
  if (organization.isError)
    return (
      <main className="p-6">
        <p role="alert" className="text-red-700">{errorTitle(organization.error)}</p>
        <Link to="/organizations" className="underline">Voltar</Link>
      </main>
    )

  return (
    <main className="mx-auto flex max-w-2xl flex-col gap-8 p-6">
      <header>
        <Link to="/organizations" className="text-sm underline">← Organizações</Link>
        <h1 className="mt-2 text-2xl font-semibold">{organization.data.name}</h1>
      </header>
      <Documents organizationId={id} />
      <Assistants organizationId={id} assistants={organization.data.assistants} />
    </main>
  )
}

function Documents({ organizationId }: { organizationId: string }) {
  const documents = useDocuments(organizationId)
  const upload = useUploadDocument(organizationId)
  const remove = useDeleteDocument(organizationId)
  const [file, setFile] = useState<File | null>(null)

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (file) upload.mutate(file)
  }

  const confirmDelete = (documentId: string, fileName: string) => {
    if (window.confirm(`Apagar o documento "${fileName}"?`)) remove.mutate(documentId)
  }

  return (
    <section className="flex flex-col gap-3">
      <h2 className="text-lg font-semibold">Documentos</h2>
      <p className="text-sm text-gray-600">Todas as IAs desta organização respondem com estes documentos.</p>
      <form onSubmit={submit} className="flex flex-col gap-3 rounded-lg border-2 border-dashed border-gray-300 bg-gray-50 p-4 sm:flex-row sm:items-end">
        <label className="flex flex-1 flex-col gap-2 text-sm font-medium text-gray-700">
          Arquivo (PDF, TXT ou MD, até 10 MB)
          <input
            type="file"
            accept=".pdf,.txt,.md"
            onChange={e => setFile(e.target.files?.[0] ?? null)}
            className="block w-full cursor-pointer text-sm text-gray-700 file:mr-3 file:cursor-pointer file:rounded file:border-0 file:bg-blue-600 file:px-4 file:py-2 file:font-medium file:text-white hover:file:bg-blue-700"
          />
        </label>
        <button
          type="submit"
          disabled={upload.isPending || !file}
          className="rounded bg-black px-5 py-2 font-medium text-white hover:bg-gray-800 disabled:cursor-not-allowed disabled:bg-gray-400"
        >
          {upload.isPending ? 'Processando...' : 'Enviar'}
        </button>
      </form>
      {upload.isError && <p role="alert" className="text-red-700">{errorTitle(upload.error)}</p>}
      {remove.isError && <p role="alert" className="text-red-700">{errorTitle(remove.error)}</p>}
      {documents.isPending && <p>Carregando...</p>}
      {documents.isError && <p role="alert" className="text-red-700">{errorTitle(documents.error)}</p>}
      <ul className="flex flex-col gap-2">
        {documents.data?.map(d => (
          <li key={d.id} className="flex items-center justify-between rounded border p-2 text-sm">
            <span>{d.fileName}</span>
            <span className="flex items-center gap-4 text-gray-600">
              {d.chunkCount} trecho(s)
              <button onClick={() => confirmDelete(d.id, d.fileName)} aria-label={`Apagar documento ${d.fileName}`} className="text-red-700">
                Apagar
              </button>
            </span>
          </li>
        ))}
      </ul>
    </section>
  )
}

function Assistants({ organizationId, assistants }: { organizationId: string; assistants: OrganizationAssistant[] }) {
  const create = useCreateAssistant()
  const remove = useDeleteAssistant()
  const [name, setName] = useState('')
  const [instructions, setInstructions] = useState('')
  const [routingDescription, setRoutingDescription] = useState('')

  const submit = (event: FormEvent) => {
    event.preventDefault()
    create.mutate(
      {
        organizationId,
        name,
        instructions: instructions.trim() === '' ? null : instructions,
        routingDescription: routingDescription.trim() === '' ? null : routingDescription,
      },
      { onSuccess: () => { setName(''); setInstructions(''); setRoutingDescription('') } },
    )
  }

  const confirmDelete = (id: string, assistantName: string) => {
    if (window.confirm(`Apagar a IA "${assistantName}"? Os documentos continuam na organização.`)) remove.mutate(id)
  }

  return (
    <section className="flex flex-col gap-3">
      <h2 className="text-lg font-semibold">IAs</h2>
      {assistants.length === 0 && <p className="text-gray-600">Nenhuma IA nesta organização</p>}
      {remove.isError && <p role="alert" className="text-red-700">{errorTitle(remove.error)}</p>}
      <ul className="flex flex-col gap-2">
        {assistants.map(a => (
          <li key={a.id} className="flex items-center justify-between rounded border p-3">
            <span className="flex flex-col">
              <Link to={`/assistants/${a.id}`} className="font-medium underline">{a.name}</Link>
              <span className="text-sm text-gray-600">{a.routingDescription ?? 'Fora do Jev'}</span>
            </span>
            <button onClick={() => confirmDelete(a.id, a.name)} aria-label={`Apagar IA ${a.name}`} className="text-sm text-red-700">
              Apagar
            </button>
          </li>
        ))}
      </ul>

      <form onSubmit={submit} className="flex flex-col gap-3 rounded border p-4">
        <h3 className="font-medium">Nova IA</h3>
        <label className="flex flex-col gap-1">
          Nome
          <input value={name} onChange={e => setName(e.target.value)} className="rounded border p-2" />
        </label>
        <label className="flex flex-col gap-1">
          Instruções
          <textarea value={instructions} onChange={e => setInstructions(e.target.value)} rows={3} className="rounded border p-2" />
        </label>
        <label className="flex flex-col gap-1">
          Quando usar esta IA
          <textarea
            value={routingDescription}
            onChange={e => setRoutingDescription(e.target.value)}
            rows={2}
            placeholder="Ex.: quando a pessoa quer entender o assunto passo a passo"
            aria-describedby="routing-help"
            className="rounded border p-2"
          />
        </label>
        <p id="routing-help" className="-mt-2 text-xs text-gray-600">
          O Jev usa isto para escolher a IA. Deixe vazio para manter a IA fora do Jev.
        </p>
        {create.isError && <p role="alert" className="text-red-700">{errorTitle(create.error)}</p>}
        <button type="submit" disabled={create.isPending} className="self-start rounded bg-black px-4 py-2 text-white disabled:opacity-50">
          {create.isPending ? 'Processando...' : 'Criar IA'}
        </button>
      </form>
    </section>
  )
}
