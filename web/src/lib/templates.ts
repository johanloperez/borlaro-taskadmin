import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { CustomFieldType, StageCategory } from '@/lib/types'

export interface TemplateStageDetail {
  name: string
  order: number
  category: StageCategory
  requiresBlockerReason: boolean
  opensReviewRound: boolean
  allowedNext: string[]
}

export interface TemplateFieldDetail {
  key: string
  label: string
  type: CustomFieldType
  options: string[]
  required: boolean
  agentHint: string | null
}

export interface TemplateDetail {
  id: string
  key: string
  name: string
  description: string
  isBuiltIn: boolean
  itemNounSingular: string
  itemNounPlural: string
  workItemTypes: string[]
  agentContext: string
  projectsUsing: number
  stages: TemplateStageDetail[]
  fields: TemplateFieldDetail[]
}

/** Lo que se manda al guardar. Sin `id` ni `projectsUsing`: son del servidor. */
export type TemplateInput = Omit<TemplateDetail, 'id' | 'isBuiltIn' | 'projectsUsing'>

export const templateKeys = {
  full: ['templates', 'full'] as const,
}

export function useTemplatesFull() {
  return useQuery({
    queryKey: templateKeys.full,
    queryFn: () => api<TemplateDetail[]>('/api/templates/full'),
  })
}

function invalidate(qc: ReturnType<typeof useQueryClient>) {
  void qc.invalidateQueries({ queryKey: templateKeys.full })
  // El alta de proyectos usa el resumen liviano: si no se invalida, una plantilla nueva no
  // aparece hasta recargar la página.
  void qc.invalidateQueries({ queryKey: ['templates'] })
}

export function useSaveTemplate() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...body }: { id?: string } & TemplateInput) =>
      id
        ? api<TemplateDetail>(`/api/templates/${id}`, { method: 'PUT', body })
        : api<TemplateDetail>('/api/templates', { method: 'POST', body }),
    onSuccess: () => invalidate(qc),
  })
}

export function useDuplicateTemplate() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      api<TemplateDetail>(`/api/templates/${id}/duplicate`, { method: 'POST' }),
    onSuccess: () => invalidate(qc),
  })
}

export function useDeleteTemplate() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api<void>(`/api/templates/${id}`, { method: 'DELETE' }),
    onSuccess: () => invalidate(qc),
  })
}

export const EMPTY_TEMPLATE: TemplateInput = {
  key: '',
  name: '',
  description: '',
  itemNounSingular: 'tarea',
  itemNounPlural: 'tareas',
  workItemTypes: ['Tarea'],
  agentContext: '',
  stages: [
    { name: 'Por hacer', order: 0, category: 'Todo', requiresBlockerReason: false, opensReviewRound: false, allowedNext: ['En curso'] },
    { name: 'En curso', order: 1, category: 'InProgress', requiresBlockerReason: false, opensReviewRound: false, allowedNext: ['Hecho'] },
    { name: 'Hecho', order: 2, category: 'Done', requiresBlockerReason: false, opensReviewRound: false, allowedNext: [] },
  ],
  fields: [],
}
