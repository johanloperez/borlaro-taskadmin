import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { AlarmClock, Bot, CalendarOff, CheckCheck, Clock, Loader2, MoveRight } from 'lucide-react'
import { AppShell } from '@/components/AppShell'
import { useFeed, useMarkFeedRead } from '@/lib/queries'
import { cn } from '@/lib/cn'
import { LOCALE, useI18n, useT, type Translate } from '@/lib/i18n'
import type { FeedItem } from '@/lib/types'

/** Cada clase de novedad con su ícono. El ícono no es decoración: en una lista larga es lo que
 *  permite saltear de un vistazo lo que no interesa hoy, y leer solo lo que sí. */
const ICONS = {
  'item.overdue': AlarmClock,
  'checkin.opened': Bot,
  'checkin.answered': CheckCheck,
  'item.time_extended': Clock,
  'item.stage_changed_by_agent': MoveRight,
  'item.started_without_date': CalendarOff,
} as const

function kindLabel(kind: string, t: Translate) {
  return kind in ICONS
    ? t(`feed.kind.${kind}` as 'feed.kind.item.overdue')
    : t('feed.kind.other')
}

/** El feed de quien lidera: tareas vencidas, el agente escribiendo, la respuesta que dio la
 *  persona, el tiempo que se agregó y las etapas que el agente movió.
 *
 *  Es un feed y no una bandeja: no se contesta desde acá. Cada novedad enlaza al lugar donde se
 *  resuelve, y el resumen de la respuesta viaja en el propio aviso —un aviso que obliga a abrir
 *  otra pantalla para saber qué pasó se ignora a la tercera vez. */
export function FeedPage() {
  const t = useT()
  const locale = LOCALE[useI18n().language]
  const navigate = useNavigate()

  const [onlyUnread, setOnlyUnread] = useState(false)
  const feed = useFeed(onlyUnread)
  const markRead = useMarkFeedRead()

  function open(row: FeedItem) {
    // Marcar y navegar, en ese orden pero sin esperar: que la marca tarde no puede demorar la
    // navegación, y si falla lo peor que pasa es que quede en negrita una novedad ya vista.
    if (!row.readAt) markRead.mutate(row.id)
    if (row.linkUrl) navigate(row.linkUrl)
  }

  return (
    <AppShell>
      <div className="mx-auto max-w-3xl px-5 py-6">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 className="text-xl font-semibold tracking-tight">{t('feed.title')}</h1>
            <p className="text-sm text-ink-muted">{t('feed.subtitle')}</p>
          </div>

          <div className="flex items-center gap-3">
            <label className="flex items-center gap-1.5 text-xs text-ink-muted">
              <input
                type="checkbox"
                checked={onlyUnread}
                onChange={(e) => setOnlyUnread(e.target.checked)}
                className="accent-accent"
              />
              {t('feed.onlyUnread')}
            </label>

            <button
              type="button"
              onClick={() => markRead.mutate(null)}
              className="rounded-md border border-line px-2.5 py-1.5 text-xs text-ink-muted
                         hover:bg-surface"
            >
              {t('feed.markAll')}
            </button>
          </div>
        </div>

        {feed.isPending ? (
          <Loader2 className="mt-8 size-5 animate-spin text-ink-subtle" />
        ) : feed.data && feed.data.length > 0 ? (
          <ul className="mt-5 space-y-1.5">
            {feed.data.map((row) => {
              const Icon = ICONS[row.kind as keyof typeof ICONS] ?? Bot
              return (
                <li key={row.id}>
                  <button
                    type="button"
                    onClick={() => open(row)}
                    className={cn(
                      'flex w-full items-start gap-3 rounded-lg border border-line p-3 text-left',
                      'hover:border-accent',
                      row.readAt ? 'bg-canvas' : 'bg-surface',
                    )}
                  >
                    <Icon
                      className={cn(
                        'mt-0.5 size-4 shrink-0',
                        row.readAt ? 'text-ink-subtle' : 'text-accent',
                      )}
                    />

                    <span className="min-w-0 flex-1">
                      <span className="flex flex-wrap items-baseline gap-x-2">
                        <span
                          className={cn(
                            'text-sm',
                            row.readAt ? 'text-ink-muted' : 'font-medium text-ink',
                          )}
                        >
                          {row.title}
                        </span>
                        <span className="text-[11px] uppercase tracking-wide text-ink-subtle">
                          {kindLabel(row.kind, t)}
                        </span>
                      </span>

                      <span className="mt-0.5 block text-xs text-ink-muted">{row.body}</span>

                      <span className="mt-1 block text-[11px] text-ink-subtle">
                        {new Date(row.createdAt).toLocaleString(locale)}
                      </span>
                    </span>
                  </button>
                </li>
              )
            })}
          </ul>
        ) : (
          <p className="mt-8 text-sm text-ink-subtle">
            {onlyUnread ? t('feed.emptyUnread') : t('feed.empty')}
          </p>
        )}
      </div>
    </AppShell>
  )
}
