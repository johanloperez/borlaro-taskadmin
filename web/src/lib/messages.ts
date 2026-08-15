import { useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { UserRole } from '@/stores/auth'

export interface ThreadSummary {
  userId: string
  name: string
  email: string
  role: UserRole
  lastMessage: string | null
  lastAt: string | null
  lastWasMine: boolean
  unread: number
}

export interface Message {
  id: string
  fromUserId: string
  fromName: string
  mine: boolean
  body: string
  createdAt: string
  readAt: string | null
}

export const messageKeys = {
  threads: ['messages', 'threads'] as const,
  thread: (id: string) => ['messages', id] as const,
  unread: ['messages', 'unread'] as const,
}

/** El contador del encabezado. Vive en su propio módulo y no en la pantalla de mensajes para
 *  que importarlo desde el shell no arrastre la pantalla entera. */
export function useUnreadMessages() {
  return useQuery({
    queryKey: messageKeys.unread,
    queryFn: () => api<{ count: number }>('/api/messages/unread'),
    refetchInterval: 60_000,
  })
}
