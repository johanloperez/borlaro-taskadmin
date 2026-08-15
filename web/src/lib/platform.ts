import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiForm } from '@/lib/api'

export interface OrganizationSummary {
  id: string
  name: string
  slug: string
  /** Ruta del logo en el almacenamiento. Null = no cargó. */
  logoPath: string | null
  isSuspended: boolean
  suspendedAt: string | null
  suspendedReason: string | null
  createdAt: string
  people: number
  projects: number
  openItems: number
  /** Última vez que alguien movió algo. Es la señal de si la organización está viva, y es todo
   *  lo que el operador ve de su actividad: no qué se movió, solo cuándo. */
  lastActivityAt: string | null
  usage: OrganizationUsage
}

/** Consumo del modelo en los últimos 30 días. Sirve para saber de quién es el costo del agente
 *  cuando lo paga la plataforma. */
export interface OrganizationUsage {
  checkIns: number
  inputTokens: number
  outputTokens: number
  cacheReadTokens: number
  /** True si la organización cargó su propia clave: entonces esos tokens no son de la casa. */
  ownModel: boolean
}

export interface TenancyInfo {
  mode: 'Single' | 'Multi'
  multi: boolean
  registrationOpen: boolean
}

export const platformKeys = {
  organizations: ['platform', 'organizations'] as const,
  tenancy: ['tenancy'] as const,
}

/** En qué modo corre la instalación. Es anónima y la usa la pantalla de entrada, así que no
 *  depende de tener sesión. */
export function useTenancy() {
  return useQuery({
    queryKey: platformKeys.tenancy,
    queryFn: () => api<TenancyInfo>('/api/tenancy'),
    staleTime: Infinity,
  })
}

export function useOrganizations() {
  return useQuery({
    queryKey: platformKeys.organizations,
    queryFn: () => api<OrganizationSummary[]>('/api/platform/organizations'),
  })
}

export function useCreateOrganization() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (form: FormData) => apiForm('/api/platform/organizations', form),
    onSuccess: () => client.invalidateQueries({ queryKey: platformKeys.organizations }),
  })
}

export function useSuspendOrganization() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) =>
      api(`/api/platform/organizations/${id}/suspender`, { method: 'POST', body: { reason } }),
    onSuccess: () => client.invalidateQueries({ queryKey: platformKeys.organizations }),
  })
}

export function useReactivateOrganization() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      api(`/api/platform/organizations/${id}/reactivar`, { method: 'POST' }),
    onSuccess: () => client.invalidateQueries({ queryKey: platformKeys.organizations }),
  })
}

/** Borrar una organización y todo lo suyo. El nombre viaja como confirmación y el servidor lo
 *  vuelve a comprobar: la confirmación del formulario evita el error, la del servidor evita que
 *  alguien llame al endpoint sin ella. */
export function useDeleteOrganization() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, name }: { id: string; name: string }) =>
      api(`/api/platform/organizations/${id}?confirmar=${encodeURIComponent(name)}`, {
        method: 'DELETE',
      }),
    onSuccess: () => client.invalidateQueries({ queryKey: platformKeys.organizations }),
  })
}
