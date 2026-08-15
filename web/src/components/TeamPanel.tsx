import { useEffect, useState } from 'react'
import { Crown, Eye, Loader2, Users, X } from 'lucide-react'
import { useProjectMembers, useSetProjectLeads } from '@/lib/admin'
import { useTeam } from '@/lib/queries'
import { ApiError } from '@/lib/api'
import { cn } from '@/lib/cn'
import { useT } from '@/lib/i18n'
import { USER_ROLE_LABEL, canLead, initials } from '@/lib/people'
import type { Participation } from '@/lib/types'

/** Quiénes participan del proyecto y quién lo lidera.
 *
 *  Participar no se edita acá: se participa teniendo trabajo asignado. Lo único que se designa
 *  es el liderazgo, porque no se puede deducir del tablero y hace falta antes de que exista la
 *  primera tarea. */
export function TeamButton({
  projectKey,
  canManage,
}: {
  projectKey: string
  canManage: boolean
}) {
  const t = useT()
  const [open, setOpen] = useState(false)
  const members = useProjectMembers(projectKey)

  return (
    <>
      <button
        onClick={() => setOpen(true)}
        title={canManage ? t('team.manageHint') : t('team.viewHint')}
        className="inline-flex items-center gap-1.5 rounded-lg px-2.5 py-1.5 text-sm text-ink-muted
                   hover:bg-canvas hover:text-ink"
      >
        <Users className="size-4" />
        {t('team.button')}
        {members.data ? ` (${members.data.length})` : ''}
      </button>

      {open && (
        <LeadsDialog projectKey={projectKey} canManage={canManage} onClose={() => setOpen(false)} />
      )}
    </>
  )
}

function LeadsDialog({
  projectKey,
  canManage,
  onClose,
}: {
  projectKey: string
  canManage: boolean
  onClose: () => void
}) {
  const t = useT()
  const everyone = useTeam()
  const members = useProjectMembers(projectKey)
  const save = useSetProjectLeads(projectKey)

  const [leads, setLeads] = useState<string[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  // Arranca en null y se siembra cuando llegan los datos: sin eso, el primer render con la lista
  // vacía se tomaría como «el proyecto se quedó sin líderes».
  useEffect(() => {
    if (members.data && leads === null) {
      setLeads(members.data.filter((m) => m.participation === 'Lead').map((m) => m.userId))
    }
  }, [members.data, leads])

  const current = leads ?? []

  async function submit() {
    setError(null)
    try {
      await save.mutateAsync(current)
      onClose()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('team.saveFailed'))
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-ink/20 p-4">
      <div className="w-full max-w-lg rounded-card border border-line bg-surface shadow-lg">
        <header className="flex items-center justify-between border-b border-line px-4 py-3">
          <h2 className="text-sm font-medium">{t('team.dialogTitle', { project: projectKey })}</h2>
          <button onClick={onClose} className="rounded-md p-1 text-ink-muted hover:bg-canvas">
            <X className="size-4" />
          </button>
        </header>

        <div className="max-h-96 space-y-4 overflow-y-auto p-4">
          <section>
            <h3 className="text-xs font-medium text-ink">{t('team.participating')}</h3>
            <p className="mt-0.5 text-[11px] text-ink-subtle">{t('team.participatingHelp')}</p>

            {members.data?.length === 0 && (
              <p className="mt-2 text-sm text-ink-muted">{t('team.nobodyYet')}</p>
            )}

            <ul className="mt-2 space-y-1.5">
              {members.data?.map((m) => (
                <li key={m.userId} className="flex items-center gap-2 text-sm">
                  <ParticipationBadge participation={m.participation} name={m.name} />
                  <span className="min-w-0 flex-1 truncate">{m.name}</span>
                  <span className="shrink-0 text-[11px] text-ink-subtle">
                    {m.participation === 'Reviewer'
                      ? t('team.reviews')
                      : t('team.itemCounts', { open: m.openItems, total: m.totalItems })}
                  </span>
                </li>
              ))}
            </ul>
          </section>

          <section>
            <h3 className="text-xs font-medium text-ink">{t('team.leadsTitle')}</h3>
            <p className="mt-0.5 text-[11px] text-ink-subtle">{t('team.leadsHelp')}</p>

            <div className="mt-2 space-y-1">
              {everyone.data?.filter((p) => canLead(p.role)).length === 0 && (
                <p className="rounded-lg border border-dashed border-line-strong p-2 text-xs text-ink-muted">
                  {t('team.noCandidates')}
                </p>
              )}

              {everyone.data?.filter((p) => canLead(p.role)).map((person) => {
                const on = current.includes(person.id)
                return (
                  <label
                    key={person.id}
                    className={cn(
                      'flex cursor-pointer items-center gap-2 rounded-lg px-2 py-1.5 text-sm',
                      on ? 'bg-accent-soft/40' : 'hover:bg-canvas',
                      !canManage && 'cursor-default',
                    )}
                  >
                    <input
                      type="checkbox"
                      checked={on}
                      disabled={!canManage}
                      onChange={() =>
                        setLeads((prev) => {
                          const base = prev ?? []
                          return on ? base.filter((id) => id !== person.id) : [...base, person.id]
                        })
                      }
                    />
                    <span className="min-w-0 flex-1 truncate">{person.name}</span>
                    <span className="shrink-0 text-[11px] text-ink-subtle">
                      {USER_ROLE_LABEL[person.role]}
                    </span>
                  </label>
                )
              })}
            </div>
          </section>
        </div>

        {error && <p className="px-4 pb-2 text-sm text-stage-blocked">{error}</p>}

        <footer className="flex items-center justify-between gap-2 border-t border-line px-4 py-3">
          {canManage ? (
            <>
              <p className="text-[11px] text-ink-subtle">
                {current.length === 0
                  ? t('team.needOneLead')
                  : t('team.leadCount', { count: current.length })}
              </p>
              <button
                onClick={submit}
                disabled={save.isPending || current.length === 0}
                className="inline-flex items-center gap-2 rounded-lg bg-accent px-3 py-1.5 text-sm
                           font-medium text-accent-ink disabled:opacity-50"
              >
                {save.isPending && <Loader2 className="size-4 animate-spin" />}
                {t('common.save')}
              </button>
            </>
          ) : (
            <p className="text-[11px] text-ink-muted">{t('team.cannotManage')}</p>
          )}
        </footer>
      </div>
    </div>
  )
}

function ParticipationBadge({
  participation,
  name,
}: {
  participation: Participation
  name: string
}) {
  const t = useT()

  if (participation === 'Reviewer') {
    return (
      <span
        className="grid size-6 shrink-0 place-items-center rounded-full border border-line text-ink-subtle"
        title={t('team.reviewsDeliverables')}
      >
        <Eye className="size-3" />
      </span>
    )
  }

  return (
    <span
      className={cn(
        'grid size-6 shrink-0 place-items-center rounded-full text-[10px] font-medium',
        participation === 'Lead'
          ? 'bg-accent text-accent-ink'
          : 'border border-line bg-canvas text-ink-muted',
      )}
      title={participation === 'Lead' ? t('team.leadsProject') : t('team.hasWork')}
    >
      {initials(name)}
    </span>
  )
}

/** El aside del tablero: quién participa y con qué rol, sin abrir nada. */
export function TeamAside({ projectKey }: { projectKey: string }) {
  const t = useT()
  const members = useProjectMembers(projectKey)

  return (
    <section>
      <h3 className="text-[11px] font-medium uppercase tracking-wide text-ink-subtle">
        {t('team.asideTitle')}
      </h3>

      {members.data?.length === 0 && (
        <p className="mt-2 text-xs text-ink-muted">{t('team.asideEmpty')}</p>
      )}

      <ul className="mt-2 space-y-2">
        {members.data?.map((m) => (
          <li key={m.userId} className="flex items-start gap-2">
            <ParticipationBadge participation={m.participation} name={m.name} />

            <span className="min-w-0 flex-1">
              <span className="flex items-center gap-1 text-sm">
                <span className="truncate">{m.name}</span>
                {m.participation === 'Lead' && <Crown className="size-3 shrink-0 text-accent" />}
              </span>
              <span className="block text-[11px] text-ink-subtle">
                {m.participation === 'Reviewer'
                  ? t('team.reviewsDeliverables')
                  : m.openItems > 0
                    ? t('team.openCount', { count: m.openItems })
                    : t('team.noOpenWork')}
              </span>
            </span>
          </li>
        ))}
      </ul>
    </section>
  )
}

