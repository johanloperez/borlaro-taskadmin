import { useRef, useState } from 'react'
import { Check, FileUp, Link2, MessageSquare, Paperclip } from 'lucide-react'
import {
  useAddDeliverableLink,
  useDeliverables,
  useOpenReview,
  useSubmitReview,
  useTeam,
} from '@/lib/queries'
import { useUploadDeliverable } from '@/lib/queries'
import { useAuth } from '@/stores/auth'
import { useT } from '@/lib/i18n'
import type { Deliverable, DeliverableVersion } from '@/lib/types'
import { cn } from '@/lib/cn'

/** Entregables con versiones y rondas de revisión: el equivalente del pull request para
 *  equipos de diseño y edición. Lo que se responde acá es "qué versión se aprobó y cuántas
 *  vueltas costó", que es la pregunta que los trackers de ingeniería no saben contestar. */
export function DeliverablesSection({ itemId }: { itemId: string }) {
  const t = useT()
  const me = useAuth((s) => s.user)
  const deliverables = useDeliverables(itemId)
  const team = useTeam()
  const upload = useUploadDeliverable(itemId)
  const addLink = useAddDeliverableLink(itemId)

  const fileInput = useRef<HTMLInputElement>(null)
  const [addingLink, setAddingLink] = useState(false)
  const [linkName, setLinkName] = useState('')
  const [linkUrl, setLinkUrl] = useState('')
  const [error, setError] = useState<string | null>(null)

  async function handleFile(file: File, deliverableId?: string) {
    setError(null)
    try {
      await upload.mutateAsync({ file, deliverableId })
    } catch (err) {
      setError(err instanceof Error ? err.message : t('deliverables.uploadFailed'))
    }
  }

  return (
    <div className="space-y-3 border-t border-line pt-4">
      <div className="flex items-center justify-between">
        <p className="text-xs font-medium text-ink-muted">Entregables</p>
        <div className="flex items-center gap-1">
          <input
            ref={fileInput}
            type="file"
            className="hidden"
            onChange={(e) => {
              const file = e.target.files?.[0]
              if (file) handleFile(file)
              e.target.value = ''
            }}
          />
          <button
            onClick={() => fileInput.current?.click()}
            disabled={upload.isPending}
            title={t('deliverables.upload')}
            className="rounded-md p-1.5 text-ink-muted hover:bg-canvas hover:text-ink disabled:opacity-50"
          >
            <FileUp className="size-4" />
          </button>
          <button
            onClick={() => setAddingLink((v) => !v)}
            title={t('deliverables.addLink')}
            className="rounded-md p-1.5 text-ink-muted hover:bg-canvas hover:text-ink"
          >
            <Link2 className="size-4" />
          </button>
        </div>
      </div>

      {addingLink && (
        <div className="space-y-2 rounded-lg border border-line p-2">
          <input
            value={linkName}
            onChange={(e) => setLinkName(e.target.value)}
            placeholder={t('deliverables.linkName')}
            className="w-full rounded-md border border-line bg-canvas px-2 py-1.5 text-sm focus:border-accent focus:outline-none"
          />
          <input
            value={linkUrl}
            onChange={(e) => setLinkUrl(e.target.value)}
            placeholder="https://…"
            className="w-full rounded-md border border-line bg-canvas px-2 py-1.5 text-sm focus:border-accent focus:outline-none"
          />
          <button
            onClick={async () => {
              setError(null)
              try {
                await addLink.mutateAsync({ name: linkName, url: linkUrl })
                setAddingLink(false)
                setLinkName('')
                setLinkUrl('')
              } catch (err) {
                setError(err instanceof Error ? err.message : t('deliverables.linkFailed'))
              }
            }}
            disabled={!linkName.trim() || !linkUrl.trim()}
            className="rounded-md bg-accent px-2.5 py-1 text-xs font-medium text-accent-ink
                       hover:bg-accent-hover disabled:opacity-50"
          >
            {t('deliverables.add')}
          </button>
        </div>
      )}

      {error && <p className="text-xs text-stage-blocked">{error}</p>}

      {deliverables.data?.length === 0 && (
        <p className="text-xs text-ink-subtle">
          {t('deliverables.empty')}
        </p>
      )}

      {deliverables.data?.map((d) => (
        <DeliverableCard
          key={d.id}
          deliverable={d}
          itemId={itemId}
          currentUserId={me?.id ?? ''}
          team={team.data ?? []}
          onUploadVersion={(file) => handleFile(file, d.id)}
        />
      ))}
    </div>
  )
}

function DeliverableCard({
  deliverable,
  itemId,
  currentUserId,
  team,
  onUploadVersion,
}: {
  deliverable: Deliverable
  itemId: string
  currentUserId: string
  team: { id: string; name: string }[]
  onUploadVersion: (file: File) => void
}) {
  const t = useT()
  const versionInput = useRef<HTMLInputElement>(null)

  return (
    <div className="rounded-lg border border-line p-2.5">
      <div className="flex items-center gap-2">
        <Paperclip className="size-3.5 shrink-0 text-ink-subtle" />
        <span className="text-sm font-medium truncate">{deliverable.name}</span>
        <span className="ml-auto shrink-0 text-[11px] text-ink-subtle">
          v{deliverable.currentVersion}
          {deliverable.reviewRoundCount > 0 && (
            <>
              {' · '}
              {deliverable.reviewRoundCount}{' '}
              {deliverable.reviewRoundCount === 1
                ? t('deliverables.roundOne')
                : t('deliverables.roundOther')}
            </>
          )}
        </span>
      </div>

      <div className="mt-2 space-y-2">
        {deliverable.versions.map((v) => (
          <VersionRow
            key={v.id}
            version={v}
            itemId={itemId}
            currentUserId={currentUserId}
            team={team}
          />
        ))}
      </div>

      {deliverable.kind === 'File' && (
        <>
          <input
            ref={versionInput}
            type="file"
            className="hidden"
            onChange={(e) => {
              const file = e.target.files?.[0]
              if (file) onUploadVersion(file)
              e.target.value = ''
            }}
          />
          <button
            onClick={() => versionInput.current?.click()}
            className="mt-2 text-[11px] text-accent hover:underline"
          >
            {t('deliverables.newVersion')}
          </button>
        </>
      )}
    </div>
  )
}

function VersionRow({
  version,
  itemId,
  currentUserId,
  team,
}: {
  version: DeliverableVersion
  itemId: string
  currentUserId: string
  team: { id: string; name: string }[]
}) {
  const t = useT()
  const openReview = useOpenReview(itemId)
  const submit = useSubmitReview(itemId)

  const [requesting, setRequesting] = useState(false)
  const [reviewerId, setReviewerId] = useState('')
  const [comments, setComments] = useState('')
  const [error, setError] = useState<string | null>(null)

  const pending = version.reviews.find((r) => r.status === 'Pending')
  const iAmReviewer = pending?.reviewerId === currentUserId

  return (
    <div
      className={cn(
        'rounded-md border px-2 py-1.5',
        version.isApproved ? 'border-stage-done/40 bg-stage-done/5' : 'border-line',
      )}
    >
      <div className="flex items-center gap-2 text-xs">
        <span className="font-mono text-ink-subtle">v{version.version}</span>
        {version.url ? (
          <a href={version.url} target="_blank" rel="noreferrer" className="truncate text-accent hover:underline">
            {version.url}
          </a>
        ) : (
          <a
            href={`/api/deliverable-versions/${version.id}/content`}
            className="truncate hover:underline"
          >
            {version.fileName}
          </a>
        )}
        {version.isApproved && (
          <span className="ml-auto inline-flex shrink-0 items-center gap-1 text-stage-done">
            <Check className="size-3" />
            Aprobada
          </span>
        )}
      </div>

      {version.reviews.map((r) => (
        <div key={r.id} className="mt-1 flex items-start gap-1.5 text-[11px]">
          <MessageSquare
            className={cn(
              'mt-0.5 size-3 shrink-0',
              r.status === 'Approved' && 'text-stage-done',
              r.status === 'ChangesRequested' && 'text-stage-progress',
              r.status === 'Pending' && 'text-stage-review',
            )}
          />
          <span className="text-ink-muted">
            {r.status === 'Pending' && `esperando a ${r.reviewerName}`}
            {r.status === 'Approved' && `${r.reviewerName} aprobó`}
            {r.status === 'ChangesRequested' && `${r.reviewerName} pidió cambios`}
            {r.commentsMd && <span className="text-ink">: {r.commentsMd}</span>}
          </span>
        </div>
      ))}

      {/* Solo el revisor asignado puede resolver: el backend lo valida igual, esto evita
          ofrecer un botón que va a fallar. */}
      {iAmReviewer && pending && (
        <div className="mt-2 space-y-1.5">
          <textarea
            rows={2}
            value={comments}
            onChange={(e) => setComments(e.target.value)}
            placeholder={t('deliverables.commentsPlaceholder')}
            className="w-full rounded-md border border-line bg-canvas px-2 py-1 text-xs focus:border-accent focus:outline-none"
          />
          <div className="flex gap-1.5">
            <button
              onClick={async () => {
                setError(null)
                try {
                  await submit.mutateAsync({ roundId: pending.id, decision: 'Approved', comments })
                } catch (err) {
                  setError(err instanceof Error ? err.message : t('deliverables.reviewFailed'))
                }
              }}
              className="rounded-md bg-stage-done px-2 py-1 text-[11px] font-medium text-white"
            >
              {t('deliverables.approve')}
            </button>
            <button
              onClick={async () => {
                setError(null)
                try {
                  await submit.mutateAsync({
                    roundId: pending.id,
                    decision: 'ChangesRequested',
                    comments,
                  })
                } catch (err) {
                  setError(err instanceof Error ? err.message : t('deliverables.reviewFailed'))
                }
              }}
              className="rounded-md border border-line px-2 py-1 text-[11px] font-medium"
            >
              {t('deliverables.requestChanges')}
            </button>
          </div>
        </div>
      )}

      {!pending && !requesting && (
        <button
          onClick={() => setRequesting(true)}
          className="mt-1 text-[11px] text-accent hover:underline"
        >
          Pedir revisión
        </button>
      )}

      {requesting && (
        <div className="mt-2 flex gap-1.5">
          <select
            value={reviewerId}
            onChange={(e) => setReviewerId(e.target.value)}
            className="flex-1 rounded-md border border-line bg-canvas px-2 py-1 text-[11px] focus:border-accent focus:outline-none"
          >
            <option value="">{t('deliverables.pickReviewer')}</option>
            {team.map((u) => (
              <option key={u.id} value={u.id}>
                {u.name}
              </option>
            ))}
          </select>
          <button
            disabled={!reviewerId}
            onClick={async () => {
              setError(null)
              try {
                await openReview.mutateAsync({ versionId: version.id, reviewerId })
                setRequesting(false)
              } catch (err) {
                setError(err instanceof Error ? err.message : t('deliverables.openReviewFailed'))
              }
            }}
            className="rounded-md bg-accent px-2 py-1 text-[11px] font-medium text-accent-ink disabled:opacity-50"
          >
            {t('deliverables.request')}
          </button>
        </div>
      )}

      {error && <p className="mt-1 text-[11px] text-stage-blocked">{error}</p>}
    </div>
  )
}
