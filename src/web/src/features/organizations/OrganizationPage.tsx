import { useState, type FormEvent } from 'react'
import { Link, NavLink, Outlet, useNavigate, useParams } from 'react-router'
import { errorTitle } from '../../shared/api/client'
import { laneColor } from '../../shared/lanes'
import { Alert, Button, inputClass } from '../../shared/ui'
import { useCreateAssistant, useDeleteAssistant } from '../assistants/api'
import { useDeleteDocument, useDeleteOrganization, useDocuments, useOrganization, useUploadDocument } from './api'
import { useCurrentOrganization } from './context'
import type { OrganizationDetail } from './types'

const tabClass = ({ isActive }: { isActive: boolean }) =>
  `border-b-2 px-1 pb-2 text-sm transition-colors ${isActive ? 'border-route font-semibold text-ink' : 'border-transparent text-muted hover:text-ink'}`

/** `/organizations/:id`: the organization's header and its two tabs, Conversa and Base. */
export function OrganizationLayout() {
  const { id = '' } = useParams()
  const organization = useOrganization(id)

  if (organization.isPending) return <p className="p-8 text-muted">Carregando...</p>
  if (organization.isError)
    return (
      <main className="flex flex-col items-start gap-3 p-8">
        <Alert>{errorTitle(organization.error)}</Alert>
        <Link to="/organizations" className="text-sm underline">Voltar</Link>
      </main>
    )

  return (
    <div className="flex min-h-screen flex-col">
      <header className="border-b border-line bg-surface px-6 pt-6 md:px-10">
        <h1 className="font-display text-2xl font-bold tracking-tight">{organization.data.name}</h1>
        <nav aria-label="Seções da organização" className="mt-4 flex gap-6">
          <NavLink to={`/organizations/${id}`} end className={tabClass}>Conversa</NavLink>
          <NavLink to={`/organizations/${id}/knowledge`} className={tabClass}>Base</NavLink>
        </nav>
      </header>
      <Outlet context={organization.data} />
    </div>
  )
}

/** `/organizations/:id/knowledge`: what the organization's AIs know and who they are. */
export function KnowledgePage() {
  const organization = useCurrentOrganization()
  return (
    <main className="mx-auto flex w-full max-w-3xl flex-col gap-12 px-6 py-8 md:px-10">
      <Documents organizationId={organization.id} />
      <Assistants organization={organization} />
      <DangerZone organization={organization} />
    </main>
  )
}

function SectionTitle({ title, hint }: { title: string; hint: string }) {
  return (
    <div>
      <h2 className="font-display text-lg font-bold">{title}</h2>
      <p className="text-sm text-muted">{hint}</p>
    </div>
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
    <section className="flex flex-col gap-4">
      <SectionTitle title="Documentos" hint="Todas as IAs desta organização respondem com estes documentos." />
      <form onSubmit={submit} className="flex flex-col gap-3 rounded-lg border border-dashed border-line bg-surface p-4 sm:flex-row sm:items-end">
        <label className="flex flex-1 flex-col gap-2 text-sm font-medium">
          Arquivo (PDF, TXT ou MD, até 10 MB)
          <input
            type="file"
            accept=".pdf,.txt,.md"
            onChange={e => setFile(e.target.files?.[0] ?? null)}
            className="block w-full cursor-pointer text-sm text-muted file:mr-3 file:cursor-pointer file:rounded-md file:border-0 file:bg-route-soft file:px-3 file:py-2 file:font-medium file:text-route"
          />
        </label>
        <Button type="submit" disabled={upload.isPending || !file}>
          {upload.isPending ? 'Processando...' : 'Enviar'}
        </Button>
      </form>
      {upload.isError && <Alert>{errorTitle(upload.error)}</Alert>}
      {remove.isError && <Alert>{errorTitle(remove.error)}</Alert>}
      {documents.isPending && <p className="text-sm text-muted">Carregando...</p>}
      {documents.isError && <Alert>{errorTitle(documents.error)}</Alert>}
      <ul className="divide-y divide-line rounded-lg border border-line bg-surface empty:hidden">
        {documents.data?.map(d => (
          <li key={d.id} className="flex items-center justify-between gap-4 px-4 py-2.5 text-sm">
            <span className="truncate font-mono text-[13px]">{d.fileName}</span>
            <span className="flex shrink-0 items-center gap-4 text-muted">
              {d.chunkCount} trecho(s)
              <button onClick={() => confirmDelete(d.id, d.fileName)} aria-label={`Apagar documento ${d.fileName}`} className="text-danger hover:underline">
                Apagar
              </button>
            </span>
          </li>
        ))}
      </ul>
    </section>
  )
}

function Assistants({ organization }: { organization: OrganizationDetail }) {
  const create = useCreateAssistant()
  const remove = useDeleteAssistant()
  const [name, setName] = useState('')
  const [instructions, setInstructions] = useState('')
  const [routingDescription, setRoutingDescription] = useState('')

  const submit = (event: FormEvent) => {
    event.preventDefault()
    create.mutate(
      {
        organizationId: organization.id,
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
    <section className="flex flex-col gap-4">
      <SectionTitle title="IAs" hint="Cada IA tem um jeito de responder. A escolha automática usa o “Quando usar” para decidir quem responde." />
      {organization.assistants.length === 0 && <p className="text-sm text-muted">Nenhuma IA nesta organização</p>}
      {remove.isError && <Alert>{errorTitle(remove.error)}</Alert>}
      <ul className="flex flex-col gap-2">
        {organization.assistants.map((a, index) => (
          <li
            key={a.id}
            className="flex items-start justify-between gap-4 rounded-lg border border-l-4 border-line bg-surface px-4 py-3"
            style={{ borderLeftColor: laneColor(index) }}
          >
            <span className="flex min-w-0 flex-col">
              <Link to={`/assistants/${a.id}`} className="font-medium hover:underline">{a.name}</Link>
              <span className="text-sm text-muted">{a.routingDescription ?? 'Fora da escolha automática'}</span>
            </span>
            <button onClick={() => confirmDelete(a.id, a.name)} aria-label={`Apagar IA ${a.name}`} className="shrink-0 text-sm text-danger hover:underline">
              Apagar
            </button>
          </li>
        ))}
      </ul>

      <form onSubmit={submit} className="flex flex-col gap-3 rounded-lg border border-line bg-surface p-5">
        <h3 className="font-medium">Nova IA</h3>
        <label className="flex flex-col gap-1 text-sm font-medium">
          Nome
          <input value={name} onChange={e => setName(e.target.value)} className={inputClass} />
        </label>
        <label className="flex flex-col gap-1 text-sm font-medium">
          Instruções
          <textarea value={instructions} onChange={e => setInstructions(e.target.value)} rows={3} placeholder="Ex.: explique com calma e dê exemplos" className={inputClass} />
        </label>
        <label className="flex flex-col gap-1 text-sm font-medium">
          Quando usar esta IA
          <textarea
            value={routingDescription}
            onChange={e => setRoutingDescription(e.target.value)}
            rows={2}
            placeholder="Ex.: quando a pessoa quer entender um processo de RH"
            aria-describedby="routing-help"
            className={inputClass}
          />
        </label>
        <p id="routing-help" className="-mt-2 text-xs text-muted">
          A escolha automática usa isto para decidir quem responde. Deixe vazio para a IA só responder quando for escolhida no “Para”.
        </p>
        {create.isError && <Alert>{errorTitle(create.error)}</Alert>}
        <Button type="submit" disabled={create.isPending} className="self-start">
          {create.isPending ? 'Processando...' : 'Criar IA'}
        </Button>
      </form>
    </section>
  )
}

function DangerZone({ organization }: { organization: OrganizationDetail }) {
  const remove = useDeleteOrganization()
  const navigate = useNavigate()

  const confirmDelete = () => {
    if (window.confirm(`Apagar a organização "${organization.name}"? As IAs, os documentos e as lacunas dela serão apagados.`))
      remove.mutate(organization.id, { onSuccess: () => navigate('/organizations') })
  }

  return (
    <section className="flex flex-col items-start gap-2 border-t border-line pt-6">
      {remove.isError && <Alert>{errorTitle(remove.error)}</Alert>}
      <Button tone="danger" onClick={confirmDelete} aria-label={`Apagar organização ${organization.name}`}>
        Apagar organização
      </Button>
    </section>
  )
}
