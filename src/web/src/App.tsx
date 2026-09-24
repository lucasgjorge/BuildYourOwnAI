import { Navigate, Route, Routes } from 'react-router'
import { AssistantPage } from './features/assistants/AssistantPage'
import { LoginPage } from './features/auth/LoginPage'
import { RegisterPage } from './features/auth/RegisterPage'
import { RequireAuth } from './features/auth/RequireAuth'
import { GapsPage } from './features/gaps/GapsPage'
import { JevPage } from './features/jev/JevPage'
import { OrganizationPage } from './features/organizations/OrganizationPage'
import { OrganizationsPage } from './features/organizations/OrganizationsPage'
import { AppLayout } from './shared/AppLayout'

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route element={<RequireAuth />}>
        <Route element={<AppLayout />}>
          <Route path="/organizations" element={<OrganizationsPage />} />
          <Route path="/organizations/:id" element={<OrganizationPage />} />
          <Route path="/assistants/:id" element={<AssistantPage />} />
          <Route path="/jev" element={<JevPage />} />
          <Route path="/gaps" element={<GapsPage />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/organizations" replace />} />
    </Routes>
  )
}
