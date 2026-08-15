import { useState } from 'react'
import { CalendarClock, ListPlus, Loader2 } from 'lucide-react'
import { AppShell } from '@/components/AppShell'
import { usePendingActions, useResolveAction, type PendingAction } from '@/lib/agent'
import { useT, type Translate } from '@/lib/i18n'

/** El nombre de la herramienta que usó el agente, en algo que se pueda leer. */
const TOOL_ICON: Record<string, typeof CalendarClock> = {
  request_date_change: CalendarClock,
  create_followup_task: ListPlus,
}

const TOOL_TITLE: Record<string, 'approvals.dateChange' | 'approvals.newTask'> = {
  request_date_change: 'approvals.dateChange',
  create_followup_task: 'approvals.newTask',
}

/** La cola de aprobaciones. Es la contracara de dejar que una IA escriba en el tablero: lo que
 *  compromete a alguien que no estuvo en la conversación —una fecha, trabajo nuevo— espera acá
 *  hasta que una persona decida. Mientras espera, no cambió nada. */
export function ApprovalsPage() {
  const t = useT()
  const pending = usePendingActions()
  const resolve = useResolveAction()
  const [rejecting, setRejecting] = useState<string | null>(null)
  const [reason, setReason] = useState('')

  return (
    <AppShell>
      <div className="mx-auto max-w-3xl px-5 py-8">
        <h1 className="text-lg font-semibold tracking-tight">{t('approvals.title')}</h1>
        <p className="text-sm text-ink-muted mt-1">{t('approvals.subtitle')}</p>

        <div className="mt-6 space-y-3">
          {pending.isLoading && (
            <p className="flex items-center gap-2 text-sm text-ink-subtle">
              <Loader2 className="size-4 animate-spin" /> {t('common.loading')}
            </p>
          )}

          {pending.data?.length === 0 && (
            <p className="text-sm text-ink-muted">{t('approvals.empty')}</p>
          )}

          {pending.data?.map((action) => (
            <Card
              key={action.id}
              t={t}
              action={action}
              busy={resolve.isPending}
              rejecting={rejecting === action.id}
              reason={reason}
              onReason={setReason}
              onApprove={() => resolve.mutate({ id: action.id, approve: true })}
              onStartReject={() => {
                setRejecting(action.id)
                setReason('')
              }}
              onConfirmReject={() => {
                resolve.mutate({ id: action.id, approve: false, reason })
                setRejecting(null)
              }}
              onCancelReject={() => setRejecting(null)}
            />
          ))}
        </div>
      </div>
    </AppShell>
  )
}

/** `checkInDate` es un día del calendario local de la persona, no un instante. Pasarlo por
 *  `new Date()` lo interpreta como medianoche UTC y en cualquier huso al oeste se muestra el día
 *  anterior — un check-in del lunes figurando como del domingo. */
function formatDay(isoDate: string) {
  const [year, month, day] = isoDate.split('-')
  return `${Number(day)}/${Number(month)}/${year}`
}

function Card({
  t,
  action,
  busy,
  rejecting,
  reason,
  onReason,
  onApprove,
  onStartReject,
  onConfirmReject,
  onCancelReject,
}: {
  t: Translate
  action: PendingAction
  busy: boolean
  rejecting: boolean
  reason: string
  onReason: (v: string) => void
  onApprove: () => void
  onStartReject: () => void
  onConfirmReject: () => void
  onCancelReject: () => void
}) {
  const Icon = TOOL_ICON[action.toolName] ?? CalendarClock
  const titleKey = TOOL_TITLE[action.toolName]
  const title = titleKey ? t(titleKey) : action.toolName

  return (
    <article className="rounded-card border border-line bg-surface p-4">
      <div className="flex items-start gap-3">
        <Icon className="size-4 mt-0.5 text-ink-subtle shrink-0" />
        <div className="min-w-0 flex-1">
          <p className="text-sm font-medium">{title}</p>
          <p className="text-sm text-ink-muted mt-1">{action.description}</p>
          <p className="text-xs text-ink-subtle mt-2">
            {t('approvals.origin', {
              person: action.personName,
              date: formatDay(action.checkInDate),
            })}
            {action.workItemReadableId ? ` · ${action.workItemReadableId} «${action.workItemTitle}»` : ''}
          </p>
        </div>
      </div>

      {rejecting ? (
        <div className="mt-3 flex gap-2">
          <input
            autoFocus
            value={reason}
            onChange={(e) => onReason(e.target.value)}
            placeholder={t('approvals.whyNot')}
            className="flex-1 rounded-card border border-line bg-canvas px-3 py-1.5 text-sm focus:border-accent"
          />
          <button
            onClick={onConfirmReject}
            disabled={busy}
            className="rounded-card border border-line px-3 py-1.5 text-sm disabled:opacity-40"
          >
            {t('approvals.reject')}
          </button>
          <button onClick={onCancelReject} className="px-2 text-sm text-ink-muted">
            {t('common.cancel')}
          </button>
        </div>
      ) : (
        <div className="mt-3 flex gap-2">
          <button
            onClick={onApprove}
            disabled={busy}
            className="rounded-card bg-accent text-accent-ink px-3 py-1.5 text-sm disabled:opacity-40"
          >
            {t('approvals.approve')}
          </button>
          <button
            onClick={onStartReject}
            disabled={busy}
            className="rounded-card border border-line px-3 py-1.5 text-sm text-ink-muted disabled:opacity-40"
          >
            {t('approvals.reject')}
          </button>
        </div>
      )}
    </article>
  )
}
