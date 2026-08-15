import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { Participation } from '@/lib/types'
import type { UserRole } from '@/stores/auth'

export interface AdminUser {
  id: string
  email: string
  name: string
  role: UserRole
  isActive: boolean
  timeZoneId: string
  checkInTime: string
  workDaysMask: number
  checkInsEnabled: boolean
  openItems: number
  createdAt: string
  /** False en las cuentas que entran solo por el proveedor externo. */
  hasPassword: boolean
  /** La cuenta del proveedor externo vinculada, si hay alguna. */
  externalLogin: string | null
}

export interface OidcInfo {
  enabled: boolean
  label: string
}

/** Si hay proveedor externo configurado. Lo usa el alta de personas para ofrecer cuentas sin
 *  contraseña: inventarle una a alguien que va a entrar con Google es una contraseña más dando
 *  vueltas por un chat, y nadie la usa nunca. */
export function useOidcInfo() {
  return useQuery({
    queryKey: ['oidc'],
    queryFn: () => api<OidcInfo>('/api/auth/oidc'),
    staleTime: 60_000,
  })
}

export interface ProjectMember {
  userId: string
  name: string
  email: string
  /** Rol en la instancia. */
  role: UserRole
  /** Cómo participa: se deduce del trabajo asignado, salvo el liderazgo, que se designa. */
  participation: Participation
  openItems: number
  totalItems: number
}

export const adminKeys = {
  users: ['admin', 'users'] as const,
  members: (key: string) => ['project', key, 'members'] as const,
}

/** Días laborables como banderas de DayOfWeek: el bit 0 es domingo, igual que en el backend.
 *  Se replica la convención en vez de mandar un array porque la columna ya es un entero y
 *  traducir en dos lugares es lo que hace que se desincronicen. */
/** Los días guardan la clave de traducción y no el texto: la inicial de cada día cambia con el
 *  idioma —lunes es «L» y Monday es «M»— y el orden de la semana no. */
export const WEEK_DAYS = [
  { bit: 1, short: 'day.monShort', label: 'day.mon' },
  { bit: 2, short: 'day.tueShort', label: 'day.tue' },
  { bit: 3, short: 'day.wedShort', label: 'day.wed' },
  { bit: 4, short: 'day.thuShort', label: 'day.thu' },
  { bit: 5, short: 'day.friShort', label: 'day.fri' },
  { bit: 6, short: 'day.satShort', label: 'day.sat' },
  { bit: 0, short: 'day.sunShort', label: 'day.sun' },
] as const

export const hasDay = (mask: number, bit: number) => (mask & (1 << bit)) !== 0
export const toggleDay = (mask: number, bit: number) => mask ^ (1 << bit)

export function useAdminUsers() {
  return useQuery({
    queryKey: adminKeys.users,
    queryFn: () => api<AdminUser[]>('/api/users'),
  })
}

export function useCreateUser() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: {
      email: string
      name: string
      /** Se omite en las cuentas que entran por el proveedor externo. */
      password?: string
      role: UserRole
      timeZoneId?: string
      checkInTime?: string
      workDaysMask?: number
      checkInsEnabled?: boolean
    }) => api<AdminUser>('/api/users', { method: 'POST', body }),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminKeys.users }),
  })
}

export function useEditUser() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...body }: { id: string } & Partial<
      Omit<AdminUser, 'id' | 'openItems' | 'createdAt' | 'email' | 'hasPassword' | 'externalLogin'>
    >) => api<AdminUser>(`/api/users/${id}`, { method: 'PATCH', body }),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminKeys.users }),
  })
}

/** Desvincula la cuenta del proveedor externo. Es la salida cuando la cuenta cambió de identidad
 *  del otro lado —se borró y se volvió a crear, y el `sub` es otro—: el próximo login vuelve a
 *  vincular por email. Sin esto, esa persona queda afuera y solo se arregla desde la base. */
export function useUnlinkExternal() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api<void>(`/api/users/${id}/external`, { method: 'DELETE' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminKeys.users }),
  })
}

export function useResetPassword() {
  return useMutation({
    mutationFn: ({ id, password }: { id: string; password: string }) =>
      api<void>(`/api/users/${id}/password`, { method: 'POST', body: { password } }),
  })
}

/** Borrado definitivo, solo para cuentas sin rastro. Si la persona trabajó, el backend lo
 *  rechaza diciendo exactamente qué dejó. */
export function useDeleteUser() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api<{ deleted: string }>(`/api/users/${id}/definitivo`, { method: 'DELETE' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminKeys.users }),
  })
}

export function useDeactivateUser() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api<void>(`/api/users/${id}`, { method: 'DELETE' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminKeys.users }),
  })
}

export function useProjectMembers(projectKey: string | undefined) {
  return useQuery({
    queryKey: adminKeys.members(projectKey ?? ''),
    queryFn: () => api<ProjectMember[]>(`/api/projects/${projectKey}/members`),
    enabled: Boolean(projectKey),
  })
}

/** Designa quiénes lideran. El resto del equipo no se edita: se arma asignando tareas. */
export function useSetProjectLeads(projectKey: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (leadIds: string[]) =>
      api<void>(`/api/projects/${projectKey}/leads`, { method: 'PUT', body: { leadIds } }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: adminKeys.members(projectKey) })
      // Cambiar los líderes puede cambiar lo que yo mismo puedo hacer en este proyecto.
      void qc.invalidateQueries({ queryKey: ['project', projectKey] })
    },
  })
}
