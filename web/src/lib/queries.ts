import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiForm, getAuthToken } from '@/lib/api'
import type {
  Attachment,
  BoardColumn,
  Deliverable,
  DifficultyLabels,
  FeedItem,
  ProjectDetail,
  ProjectSummary,
  ReviewStatus,
  TeamMember,
  TemplateSummary,
  TimeExtension,
  WorkItem,
  WorkItemDifficulty,
  WorkItemEvent,
  WorkItemPriority,
} from '@/lib/types'

export const keys = {
  templates: ['templates'] as const,
  projects: ['projects'] as const,
  project: (key: string) => ['project', key] as const,
  board: (key: string) => ['board', key] as const,
  events: (id: string) => ['events', id] as const,
  deliverables: (id: string) => ['deliverables', id] as const,
  users: ['users'] as const,
  mine: ['mine'] as const,
  extensions: (id: string) => ['extensions', id] as const,
  attachments: (id: string) => ['attachments', id] as const,
  feed: ['feed'] as const,
  feedUnread: ['feed', 'unread'] as const,
  stageResponsibles: (key: string, stageId: string) =>
    ['stage-responsibles', key, stageId] as const,
}

export interface IntakeForm {
  projectName: string
  itemNounSingular: string
  instructions: string | null
  workItemTypes: string[]
  fields: import('@/lib/types').CustomFieldDef[]
}

/** El formulario público se pide sin sesión: es la única lectura anónima del sistema. */
export function useIntakeForm(token: string | undefined) {
  return useQuery({
    queryKey: ['intake', token],
    queryFn: () => api<IntakeForm>(`/api/intake/${token}`),
    enabled: Boolean(token),
    retry: false,
  })
}

export function useSubmitIntake(token: string) {
  return useMutation({
    mutationFn: (body: {
      title: string
      description?: string
      type?: string
      submitterName: string
      submitterEmail: string
      customFields?: Record<string, unknown>
    }) => api<{ received: boolean; reference: number }>(`/api/intake/${token}`, { method: 'POST', body }),
  })
}

export function useEnableIntake(projectKey: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: { instructions?: string }) =>
      api<{ token: string; url: string }>(`/api/projects/${projectKey}/intake/enable`, {
        method: 'POST',
        body,
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: keys.project(projectKey) }),
  })
}

export function useTeam() {
  return useQuery({
    queryKey: keys.users,
    queryFn: () => api<TeamMember[]>('/api/auth/users'),
    staleTime: 5 * 60_000,
  })
}

export function useDeliverables(itemId: string | undefined) {
  return useQuery({
    queryKey: keys.deliverables(itemId ?? ''),
    queryFn: () => api<Deliverable[]>(`/api/items/${itemId}/deliverables`),
    enabled: Boolean(itemId),
  })
}

export function useUploadDeliverable(itemId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async ({ file, deliverableId, name }: { file: File; deliverableId?: string; name?: string }) => {
      // Subida por multipart: no pasa por el helper `api`, que serializa a JSON.
      const form = new FormData()
      form.append('file', file)
      if (deliverableId) form.append('deliverableId', deliverableId)
      if (name) form.append('name', name)

      const response = await fetch(`/api/items/${itemId}/deliverables/file`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${getAuthToken()}` },
        body: form,
      })

      if (!response.ok) {
        const payload = await response.json().catch(() => null)
        throw new Error(payload?.detail ?? 'No se pudo subir el archivo.')
      }
      return response.json()
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: keys.deliverables(itemId) })
      qc.invalidateQueries({ queryKey: keys.events(itemId) })
    },
  })
}

export function useAddDeliverableLink(itemId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: { name: string; url: string; deliverableId?: string }) =>
      api(`/api/items/${itemId}/deliverables/link`, { method: 'POST', body }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: keys.deliverables(itemId) })
      qc.invalidateQueries({ queryKey: keys.events(itemId) })
    },
  })
}

export function useOpenReview(itemId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ versionId, reviewerId }: { versionId: string; reviewerId: string }) =>
      api(`/api/deliverable-versions/${versionId}/reviews`, { method: 'POST', body: { reviewerId } }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: keys.deliverables(itemId) })
      qc.invalidateQueries({ queryKey: keys.events(itemId) })
    },
  })
}

export function useSubmitReview(itemId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ roundId, decision, comments }: { roundId: string; decision: ReviewStatus; comments?: string }) =>
      api(`/api/reviews/${roundId}`, { method: 'POST', body: { decision, comments } }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: keys.deliverables(itemId) })
      qc.invalidateQueries({ queryKey: keys.events(itemId) })
    },
  })
}

export function useTemplates() {
  return useQuery({
    queryKey: keys.templates,
    queryFn: () => api<TemplateSummary[]>('/api/templates'),
    staleTime: 5 * 60_000,
  })
}

export interface ProjectFilters {
  /** activos (por defecto) · archivados · todos */
  estado?: string
  q?: string
  templateId?: string
  soloMios?: boolean
}

export function useProjects(filters: ProjectFilters = {}) {
  return useQuery({
    queryKey: [...keys.projects, filters],
    queryFn: () => {
      const params = new URLSearchParams()
      if (filters.estado) params.set('estado', filters.estado)
      if (filters.q) params.set('q', filters.q)
      if (filters.templateId) params.set('templateId', filters.templateId)
      if (filters.soloMios) params.set('soloMios', 'true')

      const query = params.toString()
      return api<ProjectSummary[]>(query ? `/api/projects?${query}` : '/api/projects')
    },
  })
}

/** Borrar un proyecto se lleva el tablero entero. Pide repetir la clave: un DELETE que se
 *  dispara sin más deja el destrozo a un clic mal dado. */
export function useDeleteProject() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (key: string) =>
      api<{ items: number; deliverables: number }>(
        `/api/projects/${key}?confirmar=${encodeURIComponent(key)}`,
        { method: 'DELETE' },
      ),
    onSuccess: () => qc.invalidateQueries({ queryKey: keys.projects }),
  })
}

/** Borrar una tarea se lleva su historial. Es para errores; el trabajo que pasó se cierra
 *  moviéndolo a una etapa final. */
export function useDeleteItem(projectKey: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, readableId }: { id: string; readableId: string }) =>
      api<{ deleted: string }>(
        `/api/items/${id}?confirmar=${encodeURIComponent(readableId)}`,
        { method: 'DELETE' },
      ),
    onSuccess: () => qc.invalidateQueries({ queryKey: keys.board(projectKey) }),
  })
}

/** Archivar es el «se terminó» de un proyecto: sale de la lista por defecto y deja de admitir
 *  trabajo nuevo, pero no se borra nada. */
export function useArchiveProject() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ key, undo }: { key: string; undo?: boolean }) =>
      api<{ key: string; isArchived: boolean }>(
        `/api/projects/${key}/archive${undo ? '?undo=true' : ''}`,
        { method: 'POST' },
      ),
    onSuccess: () => qc.invalidateQueries({ queryKey: keys.projects }),
  })
}

export function useProject(key: string | undefined) {
  return useQuery({
    queryKey: keys.project(key ?? ''),
    queryFn: () => api<ProjectDetail>(`/api/projects/${key}`),
    enabled: Boolean(key),
  })
}

export function useBoard(key: string | undefined) {
  return useQuery({
    queryKey: keys.board(key ?? ''),
    queryFn: () => api<BoardColumn[]>(`/api/projects/${key}/board`),
    enabled: Boolean(key),
  })
}

export function useItemEvents(id: string | undefined) {
  return useQuery({
    queryKey: keys.events(id ?? ''),
    queryFn: () => api<WorkItemEvent[]>(`/api/items/${id}/events`),
    enabled: Boolean(id),
  })
}

export function useCreateProject() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: {
      key: string
      name: string
      description?: string
      templateId: string
      leadIds?: string[]
      difficultyLabels?: DifficultyLabels
    }) => api<{ id: string; key: string; name: string }>('/api/projects', { method: 'POST', body }),
    onSuccess: () => qc.invalidateQueries({ queryKey: keys.projects }),
  })
}

export function useCreateItem(projectKey: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: {
      title: string
      type?: string
      descriptionMd?: string
      priority?: WorkItemPriority
      dueDate?: string | null
      estimate?: number | null
      difficulty?: WorkItemDifficulty | null
      customFields?: Record<string, unknown>
    }) => api<WorkItem>(`/api/projects/${projectKey}/items`, { method: 'POST', body }),
    onSuccess: () => qc.invalidateQueries({ queryKey: keys.board(projectKey) }),
  })
}

export function useUpdateItem(projectKey: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...body }: { id: string } & Record<string, unknown>) =>
      api<WorkItem>(`/api/items/${id}`, { method: 'PATCH', body }),
    onSuccess: (item) => {
      qc.invalidateQueries({ queryKey: keys.board(projectKey) })
      qc.invalidateQueries({ queryKey: keys.events(item.id) })
    },
  })
}

/** Quién se hace cargo del trabajo que cae en una etapa.
 *
 *  Se invalida el proyecto y el tablero: el proyecto porque de ahí sale la definición de la etapa,
 *  y el tablero porque el encabezado de cada columna muestra a esa persona. */
export function useSetStageOwner(projectKey: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ stageId, assigneeId }: { stageId: string; assigneeId: string | null }) =>
      api<void>(`/api/projects/${projectKey}/stages/${stageId}/responsable`, {
        method: 'PUT',
        body: { assigneeId },
      }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: keys.project(projectKey) })
      void qc.invalidateQueries({ queryKey: keys.board(projectKey) })
    },
  })
}

export function useTransitionItem(projectKey: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({
      id,
      toStageId,
      blockerReason,
      sortOrder,
    }: {
      id: string
      toStageId: string
      blockerReason?: string
      sortOrder?: number
    }) =>
      api<WorkItem>(`/api/items/${id}/transition`, {
        method: 'POST',
        body: { toStageId, blockerReason, sortOrder },
      }),
    // Sin optimismo a propósito: el backend puede rechazar la transición (409) y una tarjeta
    // que se mueve y vuelve sola es peor que una que espera medio segundo.
    onSettled: (_data, _error, variables) => {
      qc.invalidateQueries({ queryKey: keys.board(projectKey) })
      qc.invalidateQueries({ queryKey: keys.events(variables.id) })
    },
  })
}


// ── Ampliaciones de tiempo ────────────────────────────────────────────────────

export function useTimeExtensions(itemId: string | null) {
  return useQuery({
    queryKey: keys.extensions(itemId ?? ''),
    queryFn: () => api<TimeExtension[]>(`/api/items/${itemId}/ampliaciones`),
    enabled: !!itemId,
  })
}

export function useVoidExtension(itemId: string, projectKey: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ extensionId, reason }: { extensionId: string; reason: string }) =>
      api<void>(`/api/items/${itemId}/ampliaciones/${extensionId}/anular`, {
        method: 'POST',
        body: { reason },
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: keys.extensions(itemId) })
      // El total cambió, así que el tablero y la tarea muestran otro número.
      qc.invalidateQueries({ queryKey: keys.board(projectKey) })
      qc.invalidateQueries({ queryKey: keys.events(itemId) })
    },
  })
}

// ── Novedades ─────────────────────────────────────────────────────────────────

export function useFeed(soloNoLeidas = false) {
  return useQuery({
    queryKey: [...keys.feed, soloNoLeidas],
    queryFn: () => api<FeedItem[]>(`/api/novedades?soloNoLeidas=${soloNoLeidas}`),
  })
}

export function useFeedUnread() {
  return useQuery({
    queryKey: keys.feedUnread,
    queryFn: () => api<{ count: number }>('/api/novedades/no-leidas'),
    // El contador se refresca solo: una novedad que aparece cinco minutos tarde en la campana
    // sigue sirviendo, y un socket para esto no se paga.
    refetchInterval: 60_000,
  })
}

export function useMarkFeedRead() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string | null) =>
      api<void>(id ? `/api/novedades/${id}/leida` : '/api/novedades/leidas', { method: 'POST' }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: keys.feed })
    },
  })
}

/** Renombra los tres niveles de un proyecto. Cambia las palabras, no la escala. */
export function useSetDifficultyLabels(projectKey: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (labels: DifficultyLabels) =>
      api<DifficultyLabels>(`/api/projects/${projectKey}/dificultad`, { method: 'PUT', body: labels }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: keys.project(projectKey) })
      // El tablero dibuja las etiquetas en el formulario de alta, así que también se rehace.
      qc.invalidateQueries({ queryKey: keys.board(projectKey) })
    },
  })
}

// ── Responsables de una etapa ─────────────────────────────────────────────────
//
// La lista es lo que reparte trabajo hoy: cuando una tarea llega a la etapa, se elige de acá al
// de menos carga. El titular único (`stage.defaultAssigneeId`) sigue funcionando para lo que se
// configuró antes de que la lista existiera, pero ya no se edita desde ningún lado — ver §12.

export function useStageResponsibles(projectKey: string, stageId: string, enabled: boolean) {
  return useQuery({
    queryKey: keys.stageResponsibles(projectKey, stageId),
    queryFn: () =>
      api<{ userId: string; name: string; order: number }[]>(
        `/api/projects/${projectKey}/stages/${stageId}/responsables`,
      ),
    enabled,
  })
}

export function useStageResponsibleMutation(projectKey: string, stageId: string) {
  const qc = useQueryClient()

  const invalidate = () => {
    void qc.invalidateQueries({ queryKey: keys.stageResponsibles(projectKey, stageId) })
    // El tablero muestra quién atiende cada columna, así que también se rehace.
    void qc.invalidateQueries({ queryKey: keys.board(projectKey) })
  }

  const add = useMutation({
    mutationFn: (userId: string) =>
      api<void>(`/api/projects/${projectKey}/stages/${stageId}/responsables`, {
        method: 'POST',
        body: { userId },
      }),
    onSuccess: invalidate,
  })

  const remove = useMutation({
    mutationFn: (userId: string) =>
      api<void>(`/api/projects/${projectKey}/stages/${stageId}/responsables/${userId}`, {
        method: 'DELETE',
      }),
    onSuccess: invalidate,
  })

  return { add, remove }
}

// ── Adjuntos ──────────────────────────────────────────────────────────────────

export function useAttachments(itemId: string | null) {
  return useQuery({
    queryKey: keys.attachments(itemId ?? ''),
    queryFn: () => api<Attachment[]>(`/api/items/${itemId}/adjuntos`),
    enabled: !!itemId,
  })
}

/** Sube un archivo. Usa `apiForm` y no `api`: el segundo serializa JSON, y acá hace falta
 *  multipart con el boundary que arma el navegador. */
export async function uploadAttachment(itemId: string, file: File) {
  const form = new FormData()
  form.append('file', file)

  return apiForm<{ id: string; name: string; sizeBytes: number }>(
    `/api/items/${itemId}/adjuntos`,
    form,
  )
}

export function useUploadAttachment(itemId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (file: File) => uploadAttachment(itemId, file),
    onSuccess: () => qc.invalidateQueries({ queryKey: keys.attachments(itemId) }),
  })
}

export function useDeleteAttachment(itemId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (attachmentId: string) =>
      api<void>(`/api/items/${itemId}/adjuntos/${attachmentId}`, { method: 'DELETE' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: keys.attachments(itemId) }),
  })
}
