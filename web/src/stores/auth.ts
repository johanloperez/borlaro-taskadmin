import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import { api, apiForm, setAuthToken } from '@/lib/api'
import { applyUserLanguage } from '@/lib/i18n'

export type UserRole =
  | 'Admin'
  | 'Manager'
  | 'Collaborator'
  | 'ClientReviewer'
  /** Opera la instalación: da de alta organizaciones y las suspende. No pertenece a ninguna
   *  empresa cliente, así que todas las pantallas del producto le salen vacías — no es un bug,
   *  es el aislamiento funcionando también para él. */
  | 'PlatformOperator'

export interface CurrentUser {
  id: string
  email: string
  name: string
  role: UserRole
  timeZoneId: string
  checkInTime: string
  /** Null = el idioma del navegador. Con valor, es el que la persona eligió. */
  language: string | null
  /** La empresa a la que pertenece esta persona. La interfaz la muestra al entrar. */
  organizationName: string | null
  /** Para pedir el logo de la organización al servidor. */
  organizationId: string
  /** Los permisos por persona. La interfaz los usa para no dibujar controles que darían 403. */
  canAssignTasks: boolean
  canSetDueDate: boolean
  canCreateTasks: boolean
  /** Ruta del logo en el almacenamiento. Null = no cargó logo. */
  logoPath: string | null
}

interface AuthResponse {
  token: string
  expiresAt: string
  user: CurrentUser
}

interface TicketResult {
  registration: boolean
  email: string | null
  suggestedName: string | null
  session: AuthResponse | null
}

/** Un alta a medio camino: el proveedor ya verificó quién es, falta cómo se llama su empresa. */
export interface PendingRegistration {
  ticket: string
  email: string
  suggestedName: string | null
}

interface AuthState {
  token: string | null
  expiresAt: string | null
  user: CurrentUser | null
  login: (email: string, password: string) => Promise<void>
  /** Canjea el ticket de un solo uso que deja la vuelta del proveedor externo. El token de
   *  sesión no viaja nunca en la URL: se pide por POST contra el ticket.
   *
   *  Devuelve `null` cuando la sesión quedó iniciada, y los datos del alta cuando el proveedor
   *  devolvió a alguien sin cuenta y el registro está abierto: ahí falta un paso más, ponerle
   *  nombre a la organización. */
  loginWithTicket: (ticket: string) => Promise<PendingRegistration | null>
  /** Segundo paso del alta: crea la organización y deja la sesión iniciada. El logo (si hay)
   *  viaja en el mismo pedido, por multipart. */
  registerOrganization: (ticket: string, organizationName: string, logo?: File | null) => Promise<void>
  /** Confirma un alta manual desde el enlace del correo y entra. */
  confirmSignUp: (token: string) => Promise<void>
  logout: () => void
  /** True si hay token y todavía no venció. Un token vencido en localStorage no
   *  debe dejar entrar a la app y descubrirlo recién en el primer 401. */
  isAuthenticated: () => boolean
}

export const useAuth = create<AuthState>()(
  persist(
    (set, get) => ({
      token: null,
      expiresAt: null,
      user: null,

      login: async (email, password) => {
        const result = await api<AuthResponse>('/api/auth/login', {
          method: 'POST',
          body: { email, password },
        })
        setAuthToken(result.token)
        applyUserLanguage(result.user.language)
        set({ token: result.token, expiresAt: result.expiresAt, user: result.user })
      },

      loginWithTicket: async (ticket) => {
        const result = await api<TicketResult>('/api/auth/oidc/exchange', {
          method: 'POST',
          body: { ticket },
        })

        if (result.registration) {
          return { ticket, email: result.email ?? '', suggestedName: result.suggestedName }
        }

        const session = result.session!
        setAuthToken(session.token)
        applyUserLanguage(session.user.language)
        set({ token: session.token, expiresAt: session.expiresAt, user: session.user })
        return null
      },

      registerOrganization: async (ticket, organizationName, logo) => {
        const form = new FormData()
        form.append('ticket', ticket)
        form.append('organizationName', organizationName)
        if (logo) form.append('logo', logo)

        const result = await apiForm<AuthResponse>('/api/auth/oidc/registro', form)
        setAuthToken(result.token)
        set({ token: result.token, expiresAt: result.expiresAt, user: result.user })
      },

      confirmSignUp: async (token) => {
        const result = await api<AuthResponse>('/api/auth/registro/confirmar', {
          method: 'POST',
          body: { token },
        })
        setAuthToken(result.token)
        set({ token: result.token, expiresAt: result.expiresAt, user: result.user })
      },

      logout: () => {
        setAuthToken(null)
        set({ token: null, expiresAt: null, user: null })
      },

      isAuthenticated: () => {
        const { token, expiresAt } = get()
        if (!token || !expiresAt) return false
        return new Date(expiresAt).getTime() > Date.now()
      },
    }),
    {
      name: 'taskadmin.auth',
      // Al rehidratar desde localStorage hay que reinyectar el token en el cliente HTTP:
      // el módulo api no lee del store para no acoplarse a él.
      onRehydrateStorage: () => (state) => {
        if (state?.token) setAuthToken(state.token)
      },
    },
  ),
)
