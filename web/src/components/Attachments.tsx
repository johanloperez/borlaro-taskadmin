import { useRef, useState } from 'react'
import { Loader2, Paperclip, X } from 'lucide-react'
import { useAttachments, useDeleteAttachment, useUploadAttachment } from '@/lib/queries'
import { ApiError } from '@/lib/api'
import { useT } from '@/lib/i18n'

/** Cuánto pesa, en algo que una persona pueda leer. Los bytes crudos no le dicen nada a nadie. */
function peso(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

/** Los archivos que acompañan al enunciado: el brief, una captura, las especificaciones.
 *
 *  No son entregables y por eso están acá y no en `DeliverablesSection`: el adjunto es lo que
 *  ENTRA a la tarea y el entregable lo que SALE. Mezclarlos haría que cada documento de referencia
 *  apareciera como algo pendiente de aprobar.
 *
 *  Necesita que la tarea exista, porque un adjunto cuelga de ella. En el alta, quien llama crea
 *  primero y sube después — a cambio de no inventar un almacén temporal que después hay que
 *  limpiar, con el costo que eso ya tuvo con los logos huérfanos (§14). */
export function Attachments({ itemId, canEdit }: { itemId: string; canEdit: boolean }) {
  const t = useT()
  const attachments = useAttachments(itemId)
  const upload = useUploadAttachment(itemId)
  const remove = useDeleteAttachment(itemId)

  const input = useRef<HTMLInputElement>(null)
  const [error, setError] = useState<string | null>(null)

  async function subir(files: FileList | null) {
    if (!files?.length) return
    setError(null)

    // De a uno y en serie: si uno falla, los anteriores ya están y el mensaje dice cuál fue. En
    // paralelo, un rechazo por tamaño deja sin saber qué entró y qué no.
    for (const file of Array.from(files)) {
      try {
        await upload.mutateAsync(file)
      } catch (err) {
        setError(
          `${file.name}: ${err instanceof ApiError ? err.message : t('item.attachFailed')}`,
        )
        break
      }
    }

    if (input.current) input.current.value = ''
  }

  const lista = attachments.data ?? []

  return (
    <div className="space-y-1.5">
      <div className="flex items-center justify-between">
        <label className="block text-xs font-medium text-ink-muted">{t('item.attachments')}</label>

        {canEdit && (
          <button
            type="button"
            onClick={() => input.current?.click()}
            disabled={upload.isPending}
            className="inline-flex items-center gap-1 rounded-md px-1.5 py-0.5 text-[11px]
                       text-ink-muted hover:bg-canvas hover:text-ink disabled:opacity-50"
          >
            {upload.isPending ? (
              <Loader2 className="size-3 animate-spin" />
            ) : (
              <Paperclip className="size-3" />
            )}
            {t('item.attachAdd')}
          </button>
        )}
      </div>

      <input
        ref={input}
        type="file"
        multiple
        onChange={(e) => subir(e.target.files)}
        className="hidden"
      />

      {lista.length === 0 ? (
        <p className="text-xs text-ink-subtle">{t('item.attachmentsEmpty')}</p>
      ) : (
        <ul className="space-y-1">
          {lista.map((a) => (
            <li key={a.id} className="flex items-center gap-2 text-xs">
              {/* Descarga con sesión: los adjuntos no son públicos, así que el enlace pasa por la
                  API y no por una URL de archivo suelta. */}
              <a
                href={`/api/items/${itemId}/adjuntos/${a.id}`}
                target="_blank"
                rel="noopener"
                className="min-w-0 flex-1 truncate text-accent underline underline-offset-2"
                title={a.name}
              >
                {a.name}
              </a>

              <span className="shrink-0 text-ink-subtle">{peso(a.sizeBytes)}</span>

              {canEdit && (
                <button
                  type="button"
                  onClick={() => remove.mutate(a.id)}
                  title={t('item.attachRemove')}
                  className="shrink-0 rounded p-0.5 text-ink-subtle hover:text-stage-blocked"
                >
                  <X className="size-3" />
                </button>
              )}
            </li>
          ))}
        </ul>
      )}

      {error && <p className="text-xs text-stage-blocked">{error}</p>}
    </div>
  )
}
