import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'

export type CheckInStatus = 'Pending' | 'Delivered' | 'Opened' | 'Partial' | 'Completed' | 'Missed'

export interface TodayCheckIn {
  id: string
  localDate: string
  status: CheckInStatus
  scheduledAt: string
}

export interface ChatTurn {
  role: 'user' | 'agent'
  text: string
  at: string
}

export interface ConversationView {
  checkInId: string
  status: CheckInStatus
  turnCount: number
  isClosed: boolean
  summary: string | null
  turns: ChatTurn[]
  appliedActions: string[]
  pendingActions: string[]
}

export interface PendingAction {
  id: string
  checkInId: string
  personName: string
  checkInDate: string
  toolName: string
  description: string | null
  workItemReadableId: string | null
  workItemTitle: string | null
  createdAt: string
}

export const agentKeys = {
  today: ['checkin', 'today'] as const,
  conversation: (id: string) => ['checkin', id] as const,
  pending: ['agent-actions', 'pending'] as const,
}

/** Devuelve `null` cuando no hay nada que responder: el 204 del backend viaja como undefined. */
export function useTodayCheckIn() {
  return useQuery({
    queryKey: agentKeys.today,
    queryFn: async () => (await api<TodayCheckIn | undefined>('/api/checkins/today')) ?? null,
    refetchInterval: 60_000,
  })
}

/** Abrir la conversación escribe en el servidor —marca la apertura y detiene la escalera— pero
 *  se modela como consulta porque es idempotente: pedirla dos veces devuelve el transcript tal
 *  como quedó, no una conversación nueva. Eso es justo lo que hace falta para retomar un
 *  check-in abandonado a mitad. */
export function useConversation(id: string | undefined) {
  return useQuery({
    queryKey: agentKeys.conversation(id ?? ''),
    queryFn: () => api<ConversationView>(`/api/checkins/${id}/conversation`, { method: 'POST' }),
    enabled: Boolean(id),
    staleTime: Infinity,
    refetchOnWindowFocus: false,
    retry: false,
  })
}

export function useReply(id: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (text: string) =>
      api<ConversationView>(`/api/checkins/${id}/messages`, { method: 'POST', body: { text } }),
    onSuccess: (view) => qc.setQueryData(agentKeys.conversation(id), view),
  })
}

export function useCloseCheckIn(id: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (mode: 'no-changes' | 'complete') =>
      api<ConversationView>(`/api/checkins/${id}/${mode}`, { method: 'POST' }),
    onSuccess: (view) => {
      qc.setQueryData(agentKeys.conversation(id), view)
      void qc.invalidateQueries({ queryKey: agentKeys.today })
    },
  })
}

export function usePendingActions() {
  return useQuery({
    queryKey: agentKeys.pending,
    queryFn: () => api<PendingAction[]>('/api/agent-actions/pending'),
  })
}

export function useResolveAction() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, approve, reason }: { id: string; approve: boolean; reason?: string }) => {
      if (approve) {
        await api<{ message: string }>(`/api/agent-actions/${id}/approve`, { method: 'POST' })
        return
      }
      await api<void>(`/api/agent-actions/${id}/reject`, { method: 'POST', body: { reason } })
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: agentKeys.pending }),
  })
}
