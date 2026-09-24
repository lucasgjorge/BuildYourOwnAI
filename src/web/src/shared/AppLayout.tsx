import { useState } from 'react'
import { Link, NavLink, Outlet, useNavigate } from 'react-router'
import { useLogout } from '../features/auth/api'
import { useGaps } from '../features/gaps/api'
import { useOrganizations } from '../features/organizations/api'
import { CreateOrganizationForm } from '../features/organizations/CreateOrganizationForm'

const itemClass = ({ isActive }: { isActive: boolean }) =>
  `flex items-center gap-2 rounded-md px-2 py-1.5 text-sm transition-colors ${
    isActive ? 'bg-jev-soft font-semibold text-ink' : 'text-ink/80 hover:bg-fog'
  }`

/** Shell of every authenticated page: organizations on the left, the page on the right. */
export function AppLayout() {
  const organizations = useOrganizations()
  const gaps = useGaps()
  const logout = useLogout()
  const navigate = useNavigate()
  const [creating, setCreating] = useState(false)
  const openGaps = gaps.data?.length ?? 0

  return (
    <div className="min-h-screen md:flex">
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
                <li key={o.id}>
                  <NavLink to={`/organizations/${o.id}`} className={itemClass}>
                    <span aria-hidden className="h-1.5 w-1.5 rounded-full bg-jev" />
                    {o.name}
                  </NavLink>
                </li>
              ))}
            </ul>
            {creating ? (
              <div className="mt-2 px-2">
                <CreateOrganizationForm compact />
              </div>
            ) : (
              <button onClick={() => setCreating(true)} className="rounded-md px-2 py-1.5 text-left text-sm text-jev hover:bg-jev-soft">
                + Nova organização
              </button>
            )}
          </section>

          <section className="flex flex-col gap-0.5 md:mt-auto">
            <NavLink to="/jev" className={itemClass}>Jev (todas)</NavLink>
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
        <Outlet />
      </div>
    </div>
  )
}
