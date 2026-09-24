import { useState, type FormEvent, type ReactNode } from 'react'
import { Link } from 'react-router'
import { errorTitle } from '../../shared/api/client'
import { Alert, Button, inputClass } from '../../shared/ui'
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
    <main className="mx-auto max-w-sm px-6 pt-16">
      <Link to="/" className="font-display text-lg font-bold tracking-tight">BuildYourOwnAI</Link>
      <h1 className="mt-10 mb-6 font-display text-3xl font-bold tracking-tight">{title}</h1>
      <form onSubmit={submit} className="flex flex-col gap-4 rounded-xl border border-line bg-surface p-6">
        <label className="flex flex-col gap-1 text-sm font-medium">
          E-mail
          <input type="email" required value={email} onChange={e => setEmail(e.target.value)} className={inputClass} />
        </label>
        <label className="flex flex-col gap-1 text-sm font-medium">
          Senha
          <input type="password" required value={password} onChange={e => setPassword(e.target.value)} className={inputClass} />
        </label>
        {error != null && <Alert>{errorTitle(error)}</Alert>}
        <Button type="submit" disabled={pending}>{pending ? 'Processando...' : submitLabel}</Button>
      </form>
      <div className="mt-4 text-sm text-muted">{footer}</div>
    </main>
  )
}
