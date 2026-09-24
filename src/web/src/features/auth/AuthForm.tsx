import { useState, type FormEvent, type ReactNode } from 'react'
import { errorTitle } from '../../shared/api/client'
import type { Credentials } from './api'

type Props = {
  title: string
  submitLabel: string
  pending: boolean
  error: unknown
  onSubmit: (credentials: Credentials) => void
  footer: ReactNode
}

export function AuthForm({ title, submitLabel, pending, error, onSubmit, footer }: Props) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  const submit = (event: FormEvent) => {
    event.preventDefault()
    onSubmit({ email, password })
  }

  return (
    <main className="mx-auto mt-16 max-w-sm p-6">
      <h1 className="mb-6 text-2xl font-semibold">{title}</h1>
      <form onSubmit={submit} className="flex flex-col gap-3">
        <label className="flex flex-col gap-1">
          E-mail
          <input type="email" required value={email} onChange={e => setEmail(e.target.value)} className="rounded border p-2" />
        </label>
        <label className="flex flex-col gap-1">
          Senha
          <input type="password" required value={password} onChange={e => setPassword(e.target.value)} className="rounded border p-2" />
        </label>
        {error != null && <p role="alert" className="text-red-700">{errorTitle(error)}</p>}
        <button type="submit" disabled={pending} className="rounded bg-black p-2 text-white disabled:opacity-50">
          {pending ? 'Processando...' : submitLabel}
        </button>
      </form>
      <div className="mt-4 text-sm">{footer}</div>
    </main>
  )
}
