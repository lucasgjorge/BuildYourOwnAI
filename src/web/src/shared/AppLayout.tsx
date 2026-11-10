import { useState } from 'react'
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router'
import { useLogout, useSession } from '../features/auth/api'
import { useGaps } from '../features/gaps/api'
import { useDeleteOrganization, useOrganizations } from '../features/organizations/api'
import { CreateOrganizationForm } from '../features/organizations/CreateOrganizationForm'
import { errorTitle } from './api/client'
import { Alert } from './ui'

const itemClass = ({ isActive }: { isActive: boolean }) =>
  `flex items-center gap-2 rounded-md px-2 py-1.5 text-sm transition-colors ${
    isActive ? 'bg-route-soft font-semibold text-ink' : 'text-ink/80 hover:bg-fog'
  }`

/** Shell of every authenticated page: organizations on the left, the page on the right. */
export function AppLayout() {
  const organizations = useOrganizations()
  const gaps = useGaps()
  const logout = useLogout()
  const session = useSession()
  const removeOrganization = useDeleteOrganization()
  const location = useLocation()
  const navigate = useNavigate()
  const [creating, setCreating] = useState(false)
  const openGaps = gaps.data?.length ?? 0

  return (
    // overflow-x-clip: nothing in the app may scroll the page sideways and cut the sidebar (source-preview, AC 17).
    <div data-testid="app-shell" className="min-h-screen overflow-x-clip md:flex">
      <aside className="border-b border-line bg-surface md:sticky md:top-0 md:flex md:h-screen md:w-64 md:shrink-0 md:flex-col md:border-r md:border-b-0">
        <nav aria-label="Principal" className="flex h-full flex-col gap-6 p-4">
          <Link to="/" className="font-display text-lg font-bold tracking-tight">
            BuildYourOwnAI
          </Link>

          <section className="flex flex-col gap-1">
            <h2 className="px-2 font-mono text-[11px] tracking-wider text-muted uppercase">Organizações</h2>
            {organizations.isPending && <p className="px-2 text-sm text-muted">Carregando...</p>}
            <ul className="flex flex-col gap-0.5">
              {organizations.data?.map(o => (
                <li key={o.id} className="flex items-center gap-1">
                  <NavLink to={`/organizations/${o.id}`} className={`${itemClass} min-w-0 flex-1 hover:bg-success-soft hover:text-success`}>
                    <span aria-hidden className="h-1.5 w-1.5 rounded-full bg-route" />
                    <span className="truncate">{o.name}</span>
                  </NavLink>
                  <button
                    type="button"
                    aria-label={`Excluir organização ${o.name}`}
                    title={`Excluir organização ${o.name}`}
                    disabled={removeOrganization.isPending}
                    onClick={() => {
                      removeOrganization.reset()
                      if (window.confirm(`Apagar a organização "${o.name}"? As IAs, os documentos e as lacunas dela serão apagados.`))
                        removeOrganization.mutate(o.id, {
                          onSuccess: () => {
                            if (location.pathname.startsWith(`/organizations/${o.id}`))
                              navigate('/organizations')
                          },
                        })
                    }}
                    className="flex h-8 w-8 shrink-0 items-center justify-center rounded-md text-muted transition-colors hover:bg-danger-soft hover:text-danger disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    <svg aria-hidden="true" viewBox="0 0 16 16" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="1.7">
                      <path strokeLinecap="round" d="m4 4 8 8m0-8-8 8" />
                    </svg>
                  </button>
                </li>
              ))}
            </ul>
            {removeOrganization.isError && <Alert>{errorTitle(removeOrganization.error)}</Alert>}
            {creating ? (
              <div className="mt-2 px-2">
                <CreateOrganizationForm compact />
              </div>
            ) : (
              <button onClick={() => setCreating(true)} className="rounded-md px-2 py-1.5 text-left text-sm text-route hover:bg-route-soft">
                + Nova organização
              </button>
            )}
          </section>

          <section className="flex flex-col gap-0.5 md:mt-auto">
            <NavLink to="/all" className={itemClass}>Todas as IAs</NavLink>
            <NavLink to="/gaps" className={itemClass}>{openGaps > 0 ? `Lacunas (${openGaps})` : 'Lacunas'}</NavLink>
            <button
              onClick={() => logout.mutate(undefined, { onSuccess: () => navigate('/login') })}
              className="rounded-md px-2 py-1.5 text-left text-sm text-muted hover:bg-fog"
            >
              Sair
            </button>
          </section>
        </nav>
      </aside>
      <div className="min-w-0 flex-1">
        {/* Who is logged in (user-name, AC 16-17); RequireAuth guarantees the session is loaded here. */}
        <header aria-label="Usuário logado" className="flex justify-end border-b border-line bg-surface px-6 py-2 text-sm text-muted">
          <span className="truncate font-medium text-ink">{session.data?.fullName ?? session.data?.email}</span>
        </header>
        <Outlet />
      </div>
    </div>
  )
}
