import { Link, useNavigate } from 'react-router'
import { useLogin } from './api'
import { AuthForm } from './AuthForm'

export function LoginPage() {
  const login = useLogin()
  const navigate = useNavigate()

  return (
    <AuthForm
      title="Entrar"
      submitLabel="Entrar"
      pending={login.isPending}
      error={login.error}
      onSubmit={credentials => login.mutate(credentials, { onSuccess: () => navigate('/assistants') })}
      footer={<>Não tem conta? <Link to="/register" className="underline">Criar conta</Link></>}
    />
  )
}
