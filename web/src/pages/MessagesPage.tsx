import { useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2, Send } from 'lucide-react'
import { AppShell } from '@/components/AppShell'
import { api, ApiError } from '@/lib/api'
import { cn } from '@/lib/cn'
import { LOCALE, useI18n, useT } from '@/lib/i18n'
import { messageKeys, type Message, type ThreadSummary } from '@/lib/messages'

/** Mensajes directos. Un admin ve a todo el equipo y puede empezar la conversación con
 *  cualquiera; una colaboradora ve los hilos que ya tiene y responde ahí. */
export function MessagesPage() {
  const t = useT()
  const locale = LOCALE[useI18n().language]
  const { userId } = useParams<{ userId: string }>()
  const navigate = useNavigate()
  const qc = useQueryClient()

  const threads = useQuery({
    queryKey: messageKeys.threads,
    queryFn: () => api<ThreadSummary[]>('/api/messages'),
  })

  const thread = useQuery({
    queryKey: messageKeys.thread(userId ?? ''),
    queryFn: () => api<Message[]>(`/api/messages/${userId}`),
    enabled: Boolean(userId),
  })

  const send = useMutation({
    mutationFn: (body: string) =>
      api<Message>(`/api/messages/${userId}`, { method: 'POST', body: { body } }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: messageKeys.thread(userId ?? '') })
      void qc.invalidateQueries({ queryKey: messageKeys.threads })
    },
  })

  const [draft, setDraft] = useState('')
  const [error, setError] = useState<string | null>(null)

  const selected = threads.data?.find((t) => t.userId === userId)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    const text = draft.trim()
    if (!text) return
    setDraft('')
    try {
      await send.mutateAsync(text)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('messages.sendFailed'))
      setDraft(text)
    }
  }

  return (
    <AppShell>
      <div className="mx-auto flex h-[calc(100vh-3.5rem)] max-w-5xl">
        <aside className="w-72 shrink-0 overflow-y-auto border-r border-line">
          {threads.isLoading && <p className="p-4 text-sm text-ink-muted">{t('common.loading')}</p>}

          {threads.data?.length === 0 && (
            <p className="p-4 text-sm text-ink-muted">{t('messages.emptyList')}</p>
          )}

          {/* La variable del hilo se llama `thread` y no `t`: `t` es la función de traducción, y
              con el nombre repetido la de adentro tapa a la de afuera. */}
          {threads.data?.map((thread) => (
            <button
              key={thread.userId}
              onClick={() => navigate(`/mensajes/${thread.userId}`)}
              className={cn(
                'block w-full border-b border-line px-4 py-3 text-left hover:bg-surface',
                thread.userId === userId && 'bg-surface',
              )}
            >
              <div className="flex items-center justify-between gap-2">
                <span className="truncate text-sm font-medium">{thread.name}</span>
                {thread.unread > 0 && (
                  <span className="shrink-0 rounded-full bg-accent px-1.5 text-[11px] text-accent-ink">
                    {thread.unread}
                  </span>
                )}
              </div>
              <p className="mt-0.5 truncate text-xs text-ink-muted">
                {thread.lastMessage
                  ? `${thread.lastWasMine ? t('messages.you') : ''}${thread.lastMessage}`
                  : t('messages.noMessages')}
              </p>
            </button>
          ))}
        </aside>

        <section className="flex min-w-0 flex-1 flex-col">
          {!userId ? (
            <p className="p-6 text-sm text-ink-muted">{t('messages.pickSomeone')}</p>
          ) : (
            <>
              <header className="border-b border-line px-5 py-3">
                <p className="text-sm font-medium">{selected?.name ?? t('messages.conversation')}</p>
                <p className="text-xs text-ink-subtle">{selected?.email}</p>
              </header>

              <div className="flex-1 space-y-2 overflow-y-auto p-5">
                {thread.data?.length === 0 && (
                  <p className="text-sm text-ink-muted">{t('messages.emptyThread')}</p>
                )}

                {thread.data?.map((m) => (
                  <div key={m.id} className={m.mine ? 'flex justify-end' : 'flex justify-start'}>
                    <div
                      className={cn(
                        'max-w-[80%] whitespace-pre-wrap rounded-card px-3.5 py-2.5 text-sm',
                        m.mine
                          ? 'bg-accent text-accent-ink'
                          : 'border border-line bg-surface text-ink',
                      )}
                    >
                      {m.body}
                      <span
                        className={cn(
                          'mt-1 block text-[11px]',
                          m.mine ? 'text-accent-ink/70' : 'text-ink-subtle',
                        )}
                      >
                        {new Date(m.createdAt).toLocaleString(locale, {
                          day: 'numeric',
                          month: 'short',
                          hour: '2-digit',
                          minute: '2-digit',
                        })}
                        {m.mine && (m.readAt ? t('messages.read') : t('messages.unread'))}
                      </span>
                    </div>
                  </div>
                ))}
              </div>

              <form onSubmit={submit} className="border-t border-line p-4">
                {error && <p className="mb-2 text-sm text-stage-blocked">{error}</p>}
                <div className="flex gap-2">
                  <input
                    value={draft}
                    onChange={(e) => setDraft(e.target.value)}
                    placeholder={t('messages.placeholder')}
                    className="flex-1 rounded-card border border-line bg-surface px-3 py-2 text-sm
                               focus:border-accent"
                  />
                  <button
                    type="submit"
                    disabled={send.isPending || draft.trim().length === 0}
                    className="rounded-card bg-accent p-2.5 text-accent-ink disabled:opacity-40"
                    title={t('messages.send')}
                  >
                    {send.isPending ? (
                      <Loader2 className="size-4 animate-spin" />
                    ) : (
                      <Send className="size-4" />
                    )}
                  </button>
                </div>
              </form>
            </>
          )}
        </section>
      </div>
    </AppShell>
  )
}
