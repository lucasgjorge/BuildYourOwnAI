import type { ButtonHTMLAttributes, ReactNode } from 'react'

export const inputClass =
  'w-full rounded-md border border-line bg-surface px-3 py-2 text-ink placeholder:text-muted/70 focus:border-jev focus:outline-none'

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & { tone?: 'primary' | 'quiet' | 'danger' }

export function Button({ tone = 'primary', className = '', ...props }: ButtonProps) {
  const tones = {
    primary: 'bg-ink text-surface hover:bg-ink/90 disabled:bg-muted/50',
    quiet: 'border border-line bg-surface text-ink hover:border-ink/40 disabled:text-muted',
    danger: 'border border-danger/30 bg-surface text-danger hover:bg-danger-soft disabled:opacity-50',
  }
  return (
    <button
      {...props}
      className={`inline-flex items-center justify-center gap-2 rounded-md px-4 py-2 text-sm font-medium transition-colors disabled:cursor-not-allowed ${tones[tone]} ${className}`}
    />
  )
}

export function Alert({ children }: { children: ReactNode }) {
  return (
    <p role="alert" className="rounded-md border border-danger/20 bg-danger-soft px-3 py-2 text-sm text-danger">
      {children}
    </p>
  )
}
