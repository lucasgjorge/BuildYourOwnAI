import { Link, useNavigate } from 'react-router'
import { useRegister } from './api'
import { AuthForm } from './AuthForm'

export function RegisterPage() {
  const register = useRegister()
  const navigate = useNavigate()

  return (
    <AuthForm
      title="Criar conta"
      submitLabel="Criar conta"
      pending={register.isPending}
      error={register.error}
      onSubmit={credentials => register.mutate(credentials, { onSuccess: () => navigate('/organizations') })}
      footer={<>Já tem conta? <Link to="/login" className="underline">Entrar</Link></>}
    />
  )
}
