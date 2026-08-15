import { useEffect, useState } from 'react'
import { Bot, X, Loader2, Trash2 } from 'lucide-react'
import { CustomFieldInput } from '@/components/CustomFieldInput'
import { DeliverablesSection } from '@/components/DeliverablesSection'
import { useDeleteItem, useItemEvents, useTeam, useTransitionItem, useUpdateItem } from '@/lib/queries'
import { priorityLabel, stageDot, type ProjectDetail, type WorkItem, type WorkItemPriority } from '@/lib/types'
import { ApiError } from '@/lib/api'
import { cn } from '@/lib/cn'
import { useT, type Translate } from '@/lib/i18n'

export function ItemDetailPanel({
  item,
  project,
  onClose,
}: {
  item: WorkItem
  project: ProjectDetail
  onClose: () => void
}) {
  const t = useT()
  const update = useUpdateItem(project.key)
  const transition = useTransitionItem(project.key)
  const events = useItemEvents(item.id)
  const team = useTeam()
  const remove = useDeleteItem(project.key)

  const nameOf = (id: string | null) =>
    (id && team.data?.find((p) => p.id === id)?.name) || t('item.someone')

  const [title, setTitle] = useState(item.title)
  const [priority, setPriority] = useState<WorkItemPriority>(item.priority)
  const [progress, setProgress] = useState(item.progressPct)
  const [fields, setFields] = useState<Record<string, unknown>>(item.customFields ?? {})
  const [error, setError] = useState<string | null>(null)
  const [confirmingDelete, setConfirmingDelete] = useState(false)

  // Al cambiar de tarjeta el panel debe reflejar la nueva, no arrastrar el borrador de la anterior.
  useEffect(() => {
    setTitle(item.title)
    setPriority(item.priority)
    setProgress(item.progressPct)
    setFields(item.customFields ?? {})
    setError(null)
    setConfirmingDelete(false)
  }, [item])

  async function save() {
    setError(null)
    try {
      // Se limpian las claves vacías antes de mandar: el backend rechaza un campo presente
      // con valor nulo, y "no lo completé" no es lo mismo que "lo puse en blanco".
      const clean = Object.fromEntries(
        Object.entries(fields).filter(([, v]) => v !== undefined && v !== '' && v !== null),
      )
      await update.mutateAsync({
        id: item.id,
        title,
        priority,
        progressPct: progress,
        customFields: clean,
      })
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('item.saveFailed'))
    }
  }

  const dirty =
    title !== item.title ||
    priority !== item.priority ||
    progress !== item.progressPct ||
    JSON.stringify(fields) !== JSON.stringify(item.customFields ?? {})

  return (
    <aside className="w-[26rem] shrink-0 border-l border-line bg-surface flex flex-col h-full">
      <header className="flex items-center justify-between border-b border-line px-4 h-12">
        <div className="flex items-center gap-2 min-w-0">
          <span className="font-mono text-xs text-ink-subtle">{item.readableId}</span>
          <span className="inline-flex items-center gap-1.5 text-xs text-ink-muted">
            <span className={cn('size-1.5 rounded-full', stageDot[item.stageCategory])} />
            {item.stageName}
          </span>
        </div>
        <button onClick={onClose} className="rounded-md p-1 text-ink-muted hover:bg-canvas hover:text-ink">
          <X className="size-4" />
        </button>
      </header>

      <div className="flex-1 overflow-y-auto p-4 space-y-5">
        {item.isBlocked && (
          <div className="rounded-lg border border-stage-blocked/30 bg-stage-blocked/5 p-3">
            <p className="text-xs font-medium text-stage-blocked">{t('item.blocked')}</p>
            <p className="mt-0.5 text-sm">{item.blockerReason}</p>
          </div>
        )}

        {/* Mover de etapa sin arrastrar. No es un atajo: arrastrar como única vía deja fuera
            a quien usa teclado o lector de pantalla, y el backend valida igual. */}
        <div className="space-y-1.5">
          <label className="block text-xs font-medium text-ink-muted">{t('item.stage')}</label>
          <select
            value={item.stageId}
            disabled={transition.isPending}
            onChange={async (e) => {
              const target = project.stages.find((s) => s.id === e.target.value)
              if (!target) return
              setError(null)

              const reason = target.requiresBlockerReason
                ? window.prompt(t('item.blockerPrompt', { stage: target.name }))
                : undefined
              if (target.requiresBlockerReason && !reason?.trim()) return

              try {
                await transition.mutateAsync({
                  id: item.id,
                  toStageId: target.id,
                  blockerReason: reason ?? undefined,
                })
              } catch (err) {
                setError(err instanceof ApiError ? err.message : t('item.moveFailed'))
              }
            }}
            className="w-full rounded-lg border border-line bg-canvas px-2.5 py-1.5 text-sm
                       focus:border-accent focus:outline-none"
          >
            {project.stages.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </select>
        </div>

        {/* Asignar y desasignar. Es del líder del proyecto: repartir trabajo es lo que hace un
            líder, y sin este control la asignación solo existía por API. */}
        <div className="space-y-1.5">
          <label className="block text-xs font-medium text-ink-muted">{t('item.assignee')}</label>
          {project.permissions.canAssign ? (
            <select
              value={item.assigneeId ?? ''}
              disabled={update.isPending}
              onChange={async (e) => {
                setError(null)
                const value = e.target.value
                try {
                  await update.mutateAsync(
                    value
                      ? { id: item.id, assigneeId: value }
                      : { id: item.id, clearAssignee: true },
                  )
                } catch (err) {
                  setError(err instanceof ApiError ? err.message : t('item.assignFailed'))
                }
              }}
              className={cn(
                'w-full rounded-lg bg-canvas px-2.5 py-1.5 text-sm focus:border-accent focus:outline-none',
                // Sin responsable el campo se resalta: es lo que hay que completar, y un borde
                // gris más entre otros seis no lo dice.
                item.assigneeId ? 'border border-line' : 'border border-dashed border-accent/60',
              )}
            >
              <option value="">{t('item.pickSomeone')}</option>
              {team.data?.map((person) => (
                <option key={person.id} value={person.id}>
                  {person.name}
                </option>
              ))}
            </select>
          ) : (
            <p className="rounded-lg border border-line bg-canvas px-2.5 py-1.5 text-sm text-ink-muted">
              {item.assigneeName ?? t('item.unassigned')}
              <span className="block text-[11px] text-ink-subtle">{t('item.onlyLeadAssigns')}</span>
            </p>
          )}

          {/* Qué puede hacer el responsable con su tarea. Va acá y no en una pantalla de
              permisos porque se decide en el mismo acto de asignar: «te la doy, y podés moverla».
              Por defecto puede moverla —es su trabajo diario— pero no reescribirla ni borrarla:
              eso es cambiar el encargo, y el encargo es de quien lo dio. */}
          {project.permissions.canAssign && item.assigneeId && (
            <div className="rounded-lg border border-line bg-canvas p-2.5 space-y-1.5">
              <p className="text-[11px] font-medium text-ink-muted">{t('item.ownerCan')}</p>

              {(
                [
                  ['assigneeCanMove', 'item.canMove'],
                  ['assigneeCanEdit', 'item.canEdit'],
                  ['assigneeCanDelete', 'item.canDelete'],
                ] as const
              ).map(([campo, etiqueta]) => (
                <label key={campo} className="flex items-center gap-2 text-xs">
                  <input
                    type="checkbox"
                    checked={item[campo]}
                    disabled={update.isPending}
                    onChange={async (e) => {
                      setError(null)
                      try {
                        await update.mutateAsync({ id: item.id, [campo]: e.target.checked })
                      } catch (err) {
                        setError(err instanceof ApiError ? err.message : t('item.permissionFailed'))
                      }
                    }}
                  />
                  {t(etiqueta)}
                </label>
              ))}
            </div>
          )}
        </div>

        <div className="space-y-1.5">
          <label className="block text-xs font-medium text-ink-muted">{t('item.title')}</label>
          <input
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            className="w-full rounded-lg border border-line bg-canvas px-2.5 py-1.5 text-sm
                       focus:border-accent focus:outline-none"
          />
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1.5">
            <label className="block text-xs font-medium text-ink-muted">{t('item.priority')}</label>
            <select
              value={priority}
              onChange={(e) => setPriority(e.target.value as WorkItemPriority)}
              className="w-full rounded-lg border border-line bg-canvas px-2.5 py-1.5 text-sm
                         focus:border-accent focus:outline-none"
            >
              {(Object.keys(priorityLabel) as WorkItemPriority[]).map((p) => (
                <option key={p} value={p}>
                  {priorityLabel[p]}
                </option>
              ))}
            </select>
          </div>

          <div className="space-y-1.5">
            <label className="block text-xs font-medium text-ink-muted">
              {t('item.progress', { value: progress })}
            </label>
            <input
              type="range"
              min={0}
              max={100}
              step={5}
              value={progress}
              onChange={(e) => setProgress(Number(e.target.value))}
              className="w-full accent-accent"
            />
          </div>
        </div>

        {project.customFields.length > 0 && (
          <div className="space-y-3 border-t border-line pt-4">
            <p className="text-xs font-medium text-ink-muted">{t('item.projectFields')}</p>
            {project.customFields.map((def) => (
              <div key={def.id} className="space-y-1.5">
                <label className="block text-xs font-medium">
                  {def.label}
                  {def.required && <span className="text-stage-blocked"> *</span>}
                </label>
                <CustomFieldInput
                  def={def}
                  value={fields[def.key]}
                  onChange={(v) => setFields((f) => ({ ...f, [def.key]: v }))}
                />
              </div>
            ))}
          </div>
        )}

        {error && <p className="text-sm text-stage-blocked">{error}</p>}

        <DeliverablesSection itemId={item.id} />

        <div className="border-t border-line pt-4">
          <p className="text-xs font-medium text-ink-muted">{t('item.history')}</p>
          <ul className="mt-2 space-y-2">
            {events.data?.slice(0, 20).map((e) => (
              <li key={e.id} className="flex items-start gap-2 text-xs">
                {/* Marcar visualmente lo que tocó la IA no es decorativo: es lo que permite
                    al manager auditar y revertir sus acciones. */}
                {e.actorType === 'Agent' ? (
                  <Bot className="mt-0.5 size-3.5 shrink-0 text-accent" />
                ) : (
                  <span className="mt-1.5 size-1.5 shrink-0 rounded-full bg-line-strong" />
                )}
                <span className="text-ink-muted">
                  <span className="text-ink">
                    {e.actorName ?? (e.actorType === 'Agent' ? t('item.agent') : t('item.system'))}
                  </span>{' '}
                  {describeEvent(t, e.field, e.oldValue, e.newValue, nameOf)}
                </span>
              </li>
            ))}
            {events.data?.length === 0 && (
              <li className="text-xs text-ink-subtle">{t('item.noHistory')}</li>
            )}
          </ul>
        </div>
      </div>

      <footer className="space-y-2 border-t border-line p-3">
        <button
          onClick={save}
          disabled={!dirty || update.isPending}
          className="inline-flex w-full items-center justify-center gap-2 rounded-lg bg-accent px-3 py-2
                     text-sm font-medium text-accent-ink hover:bg-accent-hover disabled:opacity-50"
        >
          {update.isPending && <Loader2 className="size-4 animate-spin" />}
          {dirty ? t('item.save') : t('item.noChanges')}
        </button>

        {/* Borrar es del líder y se lleva el historial: por eso pide confirmar el identificador
            y sugiere la alternativa correcta para trabajo que efectivamente pasó. */}
        {project.permissions.canEditAnyWork &&
          (confirmingDelete ? (
            <div className="rounded-lg border border-stage-blocked/40 bg-stage-blocked/5 p-2.5 space-y-2">
              <p className="text-xs text-ink-muted">
                {t('item.deleteWarning', { id: item.readableId })}
              </p>
              <div className="flex gap-2">
                <button
                  onClick={async () => {
                    setError(null)
                    try {
                      await remove.mutateAsync({ id: item.id, readableId: item.readableId })
                      onClose()
                    } catch (err) {
                      setError(err instanceof ApiError ? err.message : t('item.deleteFailed'))
                      setConfirmingDelete(false)
                    }
                  }}
                  disabled={remove.isPending}
                  className="inline-flex flex-1 items-center justify-center gap-1.5 rounded-lg
                             bg-stage-blocked px-3 py-1.5 text-sm font-medium text-white disabled:opacity-50"
                >
                  {remove.isPending && <Loader2 className="size-4 animate-spin" />}
                  {t('item.confirmDelete')}
                </button>
                <button
                  onClick={() => setConfirmingDelete(false)}
                  className="rounded-lg px-3 py-1.5 text-sm text-ink-muted"
                >
                  {t('common.cancel')}
                </button>
              </div>
            </div>
          ) : (
            <button
              onClick={() => setConfirmingDelete(true)}
              className="inline-flex w-full items-center justify-center gap-1.5 rounded-lg border
                         border-line px-3 py-1.5 text-sm text-ink-muted hover:text-stage-blocked"
            >
              <Trash2 className="size-4" />
              {t('item.delete', { id: item.readableId })}
            </button>
          ))}
      </footer>
    </aside>
  )
}

function describeEvent(
  t: Translate,
  field: string,
  oldValue: string | null,
  newValue: string | null,
  /** Los eventos de asignación guardan el id de la persona. Sin resolverlo, el historial decía
   *  «reasignó el item» sin decir a quién, que es justo el dato que se busca. */
  nameOf: (id: string | null) => string,
) {
  if (field === 'created') return t('item.ev.created')
  if (field === 'stage') return t('item.ev.stage', { from: oldValue ?? '', to: newValue ?? '' })
  if (field === 'progress') return t('item.ev.progress', { value: newValue ?? '' })
  if (field === 'priority') return t('item.ev.priority', { value: newValue ?? '' })
  if (field === 'title') return t('item.ev.title')
  if (field === 'description') return t('item.ev.description')
  if (field === 'due_date') {
    return t('item.ev.dueDate', { value: newValue ?? t('item.ev.noDate') })
  }
  if (field === 'assignee') {
    if (!newValue) {
      return oldValue
        ? t('item.ev.unassignedFrom', { from: nameOf(oldValue) })
        : t('item.ev.unassigned')
    }
    return oldValue
      ? t('item.ev.reassigned', { from: nameOf(oldValue), to: nameOf(newValue) })
      : t('item.ev.assigned', { to: nameOf(newValue) })
  }
  if (field === 'deliverable') return t('item.ev.deliverable', { value: newValue ?? '' })
  if (field === 'review_requested') return t('item.ev.reviewRequested', { value: newValue ?? '' })
  if (field === 'review_approved') return t('item.ev.reviewApproved', { value: newValue ?? '' })
  if (field === 'review_changes') return t('item.ev.reviewChanges', { value: newValue ?? '' })
  if (field.startsWith('field:')) {
    return t('item.ev.customField', {
      field: field.slice('field:'.length),
      value: newValue ?? '—',
    })
  }
  return t('item.ev.field', { field })
}
