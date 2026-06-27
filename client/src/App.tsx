import { Navigate, Route, Routes } from 'react-router-dom'
import { useAuth } from './auth/AuthContext'
import { AppShell } from './components/AppShell'
import { LoginPage } from './pages/LoginPage'
import { TicketsPage } from './pages/TicketsPage'
import { ChatPage } from './pages/ChatPage'
import { DashboardPage } from './pages/DashboardPage'
import { WorkspaceHubProvider } from './signalr/WorkspaceHubContext'

export default function App() {
  const { user } = useAuth()

  if (!user) return <LoginPage />

  // A conexão do hub vive enquanto o usuário estiver autenticado (envolve todas as rotas).
  return (
    <WorkspaceHubProvider>
      <Routes>
        <Route element={<AppShell />}>
          <Route index element={<Navigate to="/tickets" replace />} />
          <Route path="/tickets" element={<TicketsPage />} />
          <Route path="/tickets/:id" element={<ChatPage />} />
          {user.role === 'Manager' && <Route path="/dashboard" element={<DashboardPage />} />}
          <Route path="*" element={<Navigate to="/tickets" replace />} />
        </Route>
      </Routes>
    </WorkspaceHubProvider>
  )
}
