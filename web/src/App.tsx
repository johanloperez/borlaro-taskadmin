import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import type { ReactNode } from 'react'
import { useAuth } from '@/stores/auth'
import { LoginPage } from '@/pages/LoginPage'
import { ProjectsPage } from '@/pages/ProjectsPage'
import { BoardPage } from '@/pages/BoardPage'
import { IntakePage } from '@/pages/IntakePage'
import { CheckInPage } from '@/pages/CheckInPage'
import { ApprovalsPage } from '@/pages/ApprovalsPage'
import { UsersPage } from '@/pages/UsersPage'
import { SettingsPage } from '@/pages/SettingsPage'
import { MessagesPage } from '@/pages/MessagesPage'
import { FeedPage } from '@/pages/FeedPage'
import { TemplatesPage } from '@/pages/TemplatesPage'
import { ActivityPage } from '@/pages/ActivityPage'
import { PlatformPage } from '@/pages/PlatformPage'
import { ConfirmSignUpPage, SignUpPage } from '@/pages/SignUpPage'

function RequireAuth({ children }: { children: ReactNode }) {
  const isAuthenticated = useAuth((s) => s.isAuthenticated())
  return isAuthenticated ? <>{children}</> : <Navigate to="/login" replace />
}

/** El operador de la plataforma no pertenece a ninguna empresa: la pantalla de proyectos le sale
 *  vacía por diseño. Mandarlo a su consola evita que la primera impresión del producto sea una
 *  lista en blanco que parece un error. */
function Home() {
  const role = useAuth((s) => s.user?.role)
  return role === 'PlatformOperator' ? <Navigate to="/plataforma" replace /> : <ProjectsPage />
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        {/* Públicas: el alta existe justamente para quien todavía no tiene cuenta. */}
        <Route path="/registro" element={<SignUpPage />} />
        <Route path="/registro/confirmar" element={<ConfirmSignUpPage />} />
        {/* Pública a propósito: quien completa un brief no tiene cuenta. */}
        <Route path="/intake/:token" element={<IntakePage />} />
        <Route
          path="/"
          element={
            <RequireAuth>
              <Home />
            </RequireAuth>
          }
        />
        <Route
          path="/plataforma"
          element={
            <RequireAuth>
              <PlatformPage />
            </RequireAuth>
          }
        />
        <Route
          path="/p/:key"
          element={
            <RequireAuth>
              <BoardPage />
            </RequireAuth>
          }
        />
        {/* La misma pantalla por dos caminos: /agent-chat es la que embebe el WebView2 del
            escritorio y toma el check-in de hoy; /checkin/:id es el enlace de los emails de la
            escalera, que apunta a uno concreto. */}
        <Route
          path="/agent-chat"
          element={
            <RequireAuth>
              <CheckInPage embedded />
            </RequireAuth>
          }
        />
        <Route
          path="/checkin/:id"
          element={
            <RequireAuth>
              <CheckInPage />
            </RequireAuth>
          }
        />
        <Route
          path="/personas"
          element={
            <RequireAuth>
              <UsersPage />
            </RequireAuth>
          }
        />
        <Route
          path="/mensajes"
          element={
            <RequireAuth>
              <MessagesPage />
            </RequireAuth>
          }
        />
        <Route
          path="/novedades"
          element={
            <RequireAuth>
              <FeedPage />
            </RequireAuth>
          }
        />
        <Route
          path="/mensajes/:userId"
          element={
            <RequireAuth>
              <MessagesPage />
            </RequireAuth>
          }
        />
        <Route
          path="/actividad"
          element={
            <RequireAuth>
              <ActivityPage />
            </RequireAuth>
          }
        />
        <Route
          path="/plantillas"
          element={
            <RequireAuth>
              <TemplatesPage />
            </RequireAuth>
          }
        />
        <Route
          path="/configuracion"
          element={
            <RequireAuth>
              <SettingsPage />
            </RequireAuth>
          }
        />
        <Route
          path="/aprobaciones"
          element={
            <RequireAuth>
              <ApprovalsPage />
            </RequireAuth>
          }
        />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  )
}
