import { NavLink, Outlet, useNavigate } from 'react-router'
import { useLogout } from '../features/auth/api'
import { useGaps } from '../features/gaps/api'

const linkClass = ({ isActive }: { isActive: boolean }) => (isActive ? 'font-semibold underline' : 'hover:underline')

/** Navigation shared by every authenticated page. */
export function AppLayout() {
  const gaps = useGaps()
  const logout = useLogout()
  const navigate = useNavigate()
  const openGaps = gaps.data?.length ?? 0

  return (
    <>
      <nav aria-label="Principal" className="border-b">
        <div className="mx-auto flex max-w-2xl items-center gap-6 px-6 py-3 text-sm">
          <NavLink to="/organizations" className={linkClass}>Organizações</NavLink>
          <NavLink to="/jev" className={linkClass}>Jev</NavLink>
          <NavLink to="/gaps" className={linkClass}>{openGaps > 0 ? `Lacunas (${openGaps})` : 'Lacunas'}</NavLink>
          <button onClick={() => logout.mutate(undefined, { onSuccess: () => navigate('/login') })} className="ml-auto underline">
            Sair
          </button>
        </div>
      </nav>
      <Outlet />
    </>
  )
}
