import { useEffect, useRef, useState } from 'react'
import { useParams } from 'react-router-dom'
import { ArrowUp, Check, ExternalLink, Loader2 } from 'lucide-react'
import {
  useCloseCheckIn,
  useConversation,
  useReply,
  useTodayCheckIn,
  type ConversationView,
} from '@/lib/agent'
import { ApiError } from '@/lib/api'
import { LOCALE, useI18n, useT, type Translate } from '@/lib/i18n'

/** La conversación del check-in. Es la misma pantalla en el navegador y dentro del WebView2 de
 *  la app de escritorio: se escribe una vez y sirve para los dos, que es lo que evita mantener
 *  dos interfaces de chat que se desincronizan.
 *
 *  Sin `id` en la URL toma el check-in de hoy — la app de escritorio entra por ahí. */
export function CheckInPage({ embedded = false }: { embedded?: boolean }) {
  const t = useT()
  const { id: idFromUrl } = useParams<{ id: string }>()
  const today = useTodayCheckIn()

  const id = idFromUrl ?? today.data?.id
  const conversation = useConversation(id)

  if (!id) {
    return (
      <Frame embedded={embedded}>
        <p className="text-ink-muted text-sm">
          {today.isLoading ? t('checkIn.searching') : t('checkIn.none')}
        </p>
      </Frame>
    )
  }

  return (
    <Frame embedded={embedded}>
      <Conversation
        id={id}
        view={conversation.data}
        isOpening={conversation.isLoading}
        error={conversation.error}
      />
    </Frame>
  )
}

function Frame({ children, embedded }: { children: React.ReactNode; embedded: boolean }) {
  const t = useT()
  const locale = LOCALE[useI18n().language]

  return (
    <div className="min-h-screen bg-canvas flex flex-col">
      <header className="border-b border-line bg-surface px-5 h-14 flex items-center justify-between">
        <div>
          <span className="font-semibold tracking-tight">{t('checkIn.title')}</span>
          <span className="text-ink-subtle text-sm ml-2">
            {new Date().toLocaleDateString(locale, {
              weekday: 'long',
              day: 'numeric',
              month: 'long',
            })}
          </span>
        </div>

        {/* El botón fijo al sistema web: desde el escritorio, cualquier cosa que exceda al chat
            —revisar un entregable, ver el tablero— se abre acá. */}
        <a
          href="/"
          target={embedded ? '_blank' : undefined}
          rel="noreferrer"
          className="inline-flex items-center gap-1.5 text-sm text-ink-muted hover:text-ink rounded-md px-2.5 py-1.5 hover:bg-canvas"
        >
          <ExternalLink className="size-4" />
          {t('checkIn.toBoard')}
        </a>
      </header>

      <main className="flex-1 min-h-0 mx-auto w-full max-w-2xl px-5 py-6 flex flex-col">{children}</main>
    </div>
  )
}

function Conversation({
  id,
  view,
  isOpening,
  error,
}: {
  id: string
  view: ConversationView | undefined
  isOpening: boolean
  error: unknown
}) {
  const t: Translate = useT()
  const [draft, setDraft] = useState('')
  const reply = useReply(id)
  const close = useCloseCheckIn(id)
  const bottom = useRef<HTMLDivElement>(null)

  useEffect(() => {
    bottom.current?.scrollIntoView({ behavior: 'smooth' })
  }, [view?.turns.length, reply.isPending])

  const busy = isOpening || reply.isPending || close.isPending
  const closed = view?.isClosed ?? false

  function send() {
    const text = draft.trim()
    if (!text || busy) return
    setDraft('')
    reply.mutate(text)
  }

  const failure = error ?? reply.error ?? close.error

  return (
    <>
      <div className="flex-1 min-h-0 overflow-y-auto space-y-3 pb-4">
        {view?.turns.map((turn, i) => (
          <div key={i} className={turn.role === 'user' ? 'flex justify-end' : 'flex justify-start'}>
            <div
              className={
                turn.role === 'user'
                  ? 'max-w-[85%] rounded-card bg-accent text-accent-ink px-3.5 py-2.5 text-sm whitespace-pre-wrap'
                  : 'max-w-[85%] rounded-card bg-surface border border-line px-3.5 py-2.5 text-sm whitespace-pre-wrap'
              }
            >
              {turn.text}
            </div>
          </div>
        ))}

        {busy && (
          <div className="flex items-center gap-2 text-ink-subtle text-sm">
            <Loader2 className="size-4 animate-spin" />
            {t('checkIn.thinking')}
          </div>
        )}

        {failure && (
          <p className="text-sm text-stage-blocked">
            {failure instanceof ApiError ? failure.message : t('checkIn.failed')}
          </p>
        )}

        {/* Lo que quedó registrado se muestra acá y no en el texto del agente: la persona ve el
            efecto real sobre el tablero, no la promesa de que se hizo. */}
        {view && (view.appliedActions.length > 0 || view.pendingActions.length > 0) && (
          <div className="rounded-card border border-line bg-surface p-3.5 text-sm space-y-1.5">
            <p className="text-ink-subtle text-xs uppercase tracking-wide">{t('checkIn.recorded')}</p>
            {view.appliedActions.map((a, i) => (
              <p key={`a${i}`} className="flex gap-1.5">
                <Check className="size-4 shrink-0 text-stage-done" />
                <span className="text-ink-muted">{a}</span>
              </p>
            ))}
            {view.pendingActions.map((a, i) => (
              <p key={`p${i}`} className="text-ink-muted pl-5.5">
                ⏳ {a}
              </p>
            ))}
          </div>
        )}

        <div ref={bottom} />
      </div>

      {closed ? (
        <p className="border-t border-line pt-4 text-sm text-ink-muted">
          {t('checkIn.closed')} {view?.summary}
        </p>
      ) : (
        <div className="border-t border-line pt-4 space-y-3">
          <div className="flex gap-2">
            <textarea
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && !e.shiftKey) {
                  e.preventDefault()
                  send()
                }
              }}
              rows={2}
              placeholder={t('checkIn.placeholder')}
              className="flex-1 resize-none rounded-card border border-line bg-surface px-3 py-2 text-sm focus:border-accent"
            />
            <button
              onClick={send}
              disabled={busy || draft.trim().length === 0}
              className="self-end rounded-card bg-accent text-accent-ink p-2.5 disabled:opacity-40"
              title={t('checkIn.send')}
            >
              <ArrowUp className="size-4" />
            </button>
          </div>

          {/* La salida rápida legítima. Sin ella la persona cierra la ventana y el dato se
              pierde, que es peor que un «sin cambios» registrado. */}
          <div className="flex gap-2 text-sm">
            <button
              onClick={() => close.mutate('no-changes')}
              disabled={busy}
              className="rounded-card border border-line px-3 py-1.5 text-ink-muted hover:bg-surface disabled:opacity-40"
            >
              {t('checkIn.noChanges')}
            </button>
            {(view?.turns.length ?? 0) > 1 && (
              <button
                onClick={() => close.mutate('complete')}
                disabled={busy}
                className="rounded-card border border-line px-3 py-1.5 text-ink-muted hover:bg-surface disabled:opacity-40"
              >
                {t('checkIn.close')}
              </button>
            )}
          </div>
        </div>
      )}
    </>
  )
}
