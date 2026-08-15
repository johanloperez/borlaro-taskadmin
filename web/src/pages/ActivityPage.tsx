import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Bot, Search, User } from 'lucide-react'
import { AppShell } from '@/components/AppShell'
import { api } from '@/lib/api'
import { useProjects } from '@/lib/queries'
import { cn } from '@/lib/cn'
import { useI18n, useT, type Translate } from '@/lib/i18n'
import { LOCALE } from '@/lib/i18n'
import type { ActorType } from '@/lib/types'

interface Activity {
  id: string
  at: string
  actorType: ActorType
  actorName: string
  projectKey: string
  itemReadableId: string
  itemTitle: string
  field: string
  oldValue: string | null
  newValue: string | null
  checkInId: string | null
}

const ACTIONS = [
  { value: '', key: 'activity.filterAll' },
  { value: 'stage', key: 'activity.filterStage' },
  { value: 'assignment', key: 'activity.filterAssignment' },
  { value: 'blocker', key: 'activity.filterBlocker' },
  { value: 'fields', key: 'activity.filterFields' },
  { value: 'created', key: 'activity.filterCreated' },
] as const

/** Quién tocó qué. Los eventos ya se guardaban en cada cambio; lo que faltaba era poder mirarlos
 *  sin entrar tarea por tarea. */
export function ActivityPage() {
  const t = useT()
  const locale = LOCALE[useI18n().language]
  const projects = useProjects()

  const [projectKey, setProjectKey] = useState('')
  const [actor, setActor] = useState('')
  const [action, setAction] = useState('')
  const [days, setDays] = useState('30')
  const [q, setQ] = useState('')

  const activity = useQuery({
    queryKey: ['activity', projectKey, actor, action, days, q],
    queryFn: () => {
      const params = new URLSearchParams()
      if (projectKey) params.set('projectKey', projectKey)
      if (actor) params.set('actor', actor)
      if (action) params.set('action', action)
      if (days) params.set('days', days)
      if (q) params.set('q', q)
      return api<Activity[]>(`/api/activity?${params}`)
    },
  })

  const select = 'rounded-lg border border-line bg-surface px-2.5 py-1.5 text-sm'

  return (
    <AppShell>
      <div className="mx-auto max-w-4xl px-6 py-8">
        <h1 className="text-xl font-semibold tracking-tight">{t('activity.title')}</h1>
        <p className="mt-1 text-sm text-ink-muted">{t('activity.subtitle')}</p>

        <div className="mt-5 flex flex-wrap items-center gap-2">
          <div className="relative">
            <Search className="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-ink-subtle" />
            <input
              value={q}
              onChange={(e) => setQ(e.target.value)}
              placeholder={t('activity.search')}
              className="w-56 rounded-lg border border-line bg-surface py-1.5 pl-8 pr-2.5 text-sm"
            />
          </div>

          <select value={projectKey} onChange={(e) => setProjectKey(e.target.value)} className={select}>
            <option value="">{t('activity.allProjects')}</option>
            {projects.data?.map((p) => (
              <option key={p.id} value={p.key}>
                {p.key} — {p.name}
              </option>
            ))}
          </select>

          <select value={action} onChange={(e) => setAction(e.target.value)} className={select}>
            {ACTIONS.map((a) => (
              <option key={a.value} value={a.value}>
                {t(a.key)}
              </option>
            ))}
          </select>

          <select value={actor} onChange={(e) => setActor(e.target.value)} className={select}>
            <option value="">{t('activity.anyone')}</option>
            <option value="User">{t('activity.actorPeople')}</option>
            <option value="Agent">{t('activity.actorAgent')}</option>
            <option value="System">{t('activity.actorSystem')}</option>
          </select>

          <select value={days} onChange={(e) => setDays(e.target.value)} className={select}>
            <option value="1">{t('activity.today')}</option>
            <option value="7">{t('activity.days7')}</option>
            <option value="30">{t('activity.days30')}</option>
            <option value="">{t('activity.filterAll')}</option>
          </select>
        </div>

        <div className="mt-6 space-y-1">
          {activity.isLoading && <p className="text-sm text-ink-muted">{t('common.loading')}</p>}

          {activity.data?.length === 0 && (
            <p className="rounded-card border border-dashed border-line-strong p-8 text-center text-sm text-ink-muted">
              {t('activity.empty')}
            </p>
          )}

          {activity.data?.map((e) => (
            <article
              key={e.id}
              className="flex items-start gap-3 rounded-lg border border-line bg-surface px-3 py-2.5"
            >
              <span
                className={cn(
                  'mt-0.5 grid size-6 shrink-0 place-items-center rounded-full',
                  e.actorType === 'Agent'
                    ? 'bg-accent-soft text-ink'
                    : 'border border-line bg-canvas text-ink-muted',
                )}
                title={e.actorName}
              >
                {e.actorType === 'Agent' ? <Bot className="size-3.5" /> : <User className="size-3.5" />}
              </span>

              <div className="min-w-0 flex-1">
                <p className="text-sm">
                  <span className="font-medium">{e.actorName}</span> {describe(t, e)}
                </p>
                <p className="mt-0.5 text-xs text-ink-muted">
                  <Link to={`/p/${e.projectKey}`} className="font-mono hover:text-ink">
                    {e.itemReadableId}
                  </Link>{' '}
                  · {e.itemTitle}
                  {e.checkInId && t('activity.fromCheckIn')}
                </p>
              </div>

              <time
                className="shrink-0 text-[11px] text-ink-subtle"
                title={new Date(e.at).toLocaleString(locale)}
              >
                {relative(t, locale, e.at)}
              </time>
            </article>
          ))}
        </div>
      </div>
    </AppShell>
  )
}

/** El texto se arma acá y no en el backend porque es presentación: el evento guarda el campo, el
 *  valor viejo y el nuevo, que es lo que hay que poder auditar. */
function describe(t: Translate, e: Activity) {
  switch (e.field) {
    case 'created':
      return t('activity.desc.created')
    case 'stage':
      return t('activity.desc.stage', { from: e.oldValue ?? '', to: e.newValue ?? '' })
    case 'assignee':
      if (e.newValue) {
        return e.oldValue
          ? t('activity.desc.reassigned', { from: e.oldValue, to: e.newValue })
          : t('activity.desc.assigned', { to: e.newValue })
      }
      return e.oldValue
        ? t('activity.desc.unassignedFrom', { from: e.oldValue })
        : t('activity.desc.unassigned')
    case 'blocker':
      return t('activity.desc.blocker', { value: e.newValue ?? '' })
    case 'progress':
      return t('activity.desc.progress', { from: e.oldValue ?? '', to: e.newValue ?? '' })
    case 'due_date':
      return e.newValue
        ? t('activity.desc.dueDate', { value: e.newValue })
        : t('activity.desc.dueDateCleared')
    case 'priority':
      return t('activity.desc.priority', { value: e.newValue ?? '' })
    case 'title':
      return t('activity.desc.title')
    case 'description':
      return t('activity.desc.description')
    case 'estimate':
      return t('activity.desc.estimate', { value: e.newValue ?? '' })
    case 'agent_note':
      return t('activity.desc.agentNote', { value: e.newValue ?? '' })
    default:
      return e.field.startsWith('field:')
        ? t('activity.desc.customField', {
            field: e.field.slice(6),
            value: e.newValue ? ` → ${e.newValue.replace(/^"|"$/g, '')}` : '',
          })
        : t('activity.desc.field', { field: e.field })
  }
}

function relative(t: Translate, locale: string, iso: string) {
  const minutes = Math.round((Date.now() - new Date(iso).getTime()) / 60000)
  if (minutes < 1) return t('activity.justNow')
  if (minutes < 60) return t('activity.minutesAgo', { n: minutes })
  const hours = Math.round(minutes / 60)
  if (hours < 24) return t('activity.hoursAgo', { n: hours })
  return new Date(iso).toLocaleDateString(locale, { day: 'numeric', month: 'short' })
}
