import { Navigate, Route, Routes } from 'react-router'
import { AssistantPage } from './features/assistants/AssistantPage'
import { AssistantsPage } from './features/assistants/AssistantsPage'
import { LoginPage } from './features/auth/LoginPage'
import { RegisterPage } from './features/auth/RegisterPage'
import { RequireAuth } from './features/auth/RequireAuth'

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route element={<RequireAuth />}>
        <Route path="/assistants" element={<AssistantsPage />} />
        <Route path="/assistants/:id" element={<AssistantPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/assistants" replace />} />
    </Routes>
  )
}
