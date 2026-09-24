import { Navigate, Outlet } from 'react-router'
import { ApiError, errorTitle } from '../../shared/api/client'
import { useSession } from './api'

/** Single guard for every authenticated route: no session (401) sends the user to /login. */
export function RequireAuth() {
  const session = useSession()

  if (session.isPending) return <p className="p-6">Carregando...</p>
  if (session.error instanceof ApiError && session.error.status === 401) return <Navigate to="/login" replace />
  if (session.isError) return <p role="alert" className="p-6 text-red-700">{errorTitle(session.error)}</p>

  return <Outlet />
}
