import { useRef, useState, type FormEvent } from 'react'
import { Loader2, Paperclip, X } from 'lucide-react'
import { CustomFieldInput } from '@/components/CustomFieldInput'
import { Markdown } from '@/components/Markdown'
import { useCreateItem, uploadAttachment } from '@/lib/queries'
import { ApiError } from '@/lib/api'
import { cn } from '@/lib/cn'
import { useT } from '@/lib/i18n'
import { useAuth } from '@/stores/auth'
import {
  difficultyLabel,
  type CustomFieldDef,
  type DifficultyLabels,
  type WorkItemDifficulty,
} from '@/lib/types'

/** El alta completa de una tarea: donde se describe bien todo.
 *
 *  **Modal grande y no una pantalla propia.** Crear una tarea es una sub-acción de estar mirando
 *  el tablero: se quiere volver exactamente a donde se estaba, y ver alrededor mientras se
 *  escribe. Un takeover de pantalla pierde ese contexto y obliga a navegar de vuelta.
 *
 *  Convive con el alta rápida en línea, que no se reemplazó: anotar algo en dos segundos desde la
 *  columna es un caso real y distinto de sentarse a redactar un brief. Es la misma división que
 *  hacen Linear y GitHub, y por el mismo motivo.
 *
 *  **Los adjuntos se suben después de crear**, en la misma acción y sin que se note. Un archivo no
 *  puede colgar de una tarea que todavía no existe, y la alternativa —un almacén temporal— ya
 *  demostró su costo con los logos huérfanos de §14: caminos donde el archivo queda escrito y la
 *  fila no se crea, cada uno con su limpieza. Acá, si la tarea se crea, los archivos tienen dónde
 *  ir; y si un archivo falla, la tarea ya existe y se dice cuál falló. */
export function CreateItemDialog({
  projectKey,
  types,
  fields,
  labels,
  onDone,
  onCreated,
  fullPage,
}: {
  projectKey: string
  types: string[]
  fields: CustomFieldDef[]
  labels: DifficultyLabels
  onDone: () => void
  onCreated: (id: string) => void
  /** Ocupa el lugar del tablero en vez de flotar encima. Una descripción con capturas y listas
   *  necesita ancho de verdad, y a 1280 px un modal centrado deja la mitad de la pantalla en
   *  fondo oscurecido. */
  fullPage?: boolean
}) {
  const t = useT()
  const create = useCreateItem(projectKey)
  const puedeFechar = useAuth((s) => s.user)?.canSetDueDate !== false

  const [title, setTitle] = useState('')
  const [type, setType] = useState(types[0] ?? 'Tarea')
  const [description, setDescription] = useState('')
  const [previewing, setPreviewing] = useState(false)
  const [dueDate, setDueDate] = useState('')
  const [estimate, setEstimate] = useState('')
  const [difficulty, setDifficulty] = useState<'' | WorkItemDifficulty>('')
  const [values, setValues] = useState<Record<string, unknown>>({})
  const [archivos, setArchivos] = useState<File[]>([])
  const [error, setError] = useState<string | null>(null)
  const [subiendo, setSubiendo] = useState(false)

  const input = useRef<HTMLInputElement>(null)
  const required = fields.filter((f) => f.required)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)

    try {
      const creada = await create.mutateAsync({
        title,
        type,
        descriptionMd: description || undefined,
        dueDate: dueDate || null,
        estimate: estimate === '' ? null : Number(estimate),
        difficulty: difficulty as WorkItemDifficulty,
        customFields: required.length > 0 ? values : undefined,
      })

      // Los archivos van de a uno y en serie: si uno falla, el mensaje dice cuál, y los
      // anteriores ya están. En paralelo no se sabría qué entró.
      if (archivos.length > 0) {
        setSubiendo(true)

        for (const file of archivos) {
          try {
            await uploadAttachment(creada.id, file)
          } catch (err) {
            // La tarea ya existe: no se pierde nada, y se dice exactamente qué faltó.
            setError(
              `${t('board.createdButAttachFailed')} ${file.name}: ${
                err instanceof ApiError ? err.message : ''
              }`,
            )
            setSubiendo(false)
            onCreated(creada.id)
            return
          }
        }
      }

      onCreated(creada.id)
      onDone()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('board.createFailed'))
    } finally {
      setSubiendo(false)
    }
  }

  const trabajando = create.isPending || subiendo

  return (
    <div
      className={cn(
        fullPage
          ? 'flex h-full min-h-0 flex-col overflow-y-auto bg-canvas'
          : 'fixed inset-0 z-40 flex items-start justify-center overflow-y-auto bg-ink/30 p-4 sm:p-8',
      )}
    >
      <form
        onSubmit={handleSubmit}
        className={cn(
          'w-full border-line bg-surface',
          fullPage ? 'flex-1' : 'max-w-6xl rounded-card border shadow-lg',
        )}
      >
        <div className="flex items-center justify-between border-b border-line px-5 py-3">
          <h2 className="text-sm font-semibold">{t('board.createTitle')}</h2>
          <button
            type="button"
            onClick={onDone}
            className="rounded-md p-1 text-ink-muted hover:bg-canvas hover:text-ink"
          >
            <X className="size-4" />
          </button>
        </div>

        {/* Dos columnas: el enunciado a la izquierda con todo el espacio, los datos a la derecha.
            Apiladas verticalmente, la descripción quedaría lejos de lo que la califica. */}
        <div
          className={cn(
            'grid gap-5 p-5',
            fullPage ? 'lg:grid-cols-[1fr_20rem]' : 'lg:grid-cols-[1fr_16rem]',
          )}
        >
          <div className="space-y-3">
            <div className="space-y-1.5">
              <label className="block text-xs font-medium text-ink-muted">{t('item.title')}</label>
              <input
                autoFocus
                required
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                placeholder={t('board.newTitle')}
                className="w-full rounded-lg border border-line bg-canvas px-2.5 py-2 text-sm
                           focus:border-accent focus:outline-none"
              />
            </div>

            <div className="space-y-1.5">
              <div className="flex items-center justify-between">
                <label className="block text-xs font-medium text-ink-muted">
                  {t('item.description')}
                </label>
                <button
                  type="button"
                  onClick={() => setPreviewing((v) => !v)}
                  className="rounded-md px-1.5 py-0.5 text-[11px] text-ink-muted hover:bg-canvas hover:text-ink"
                >
                  {previewing ? t('item.descriptionEdit') : t('item.descriptionPreview')}
                </button>
              </div>

              {previewing ? (
                <div
                  className={cn(
                    'rounded-lg border border-line bg-canvas px-2.5 py-2 text-sm',
                    fullPage ? 'min-h-[32rem]' : 'min-h-64',
                  )}
                >
                  {description.trim() ? (
                    <Markdown>{description}</Markdown>
                  ) : (
                    <p className="text-ink-subtle">{t('item.descriptionEmpty')}</p>
                  )}
                </div>
              ) : (
                <textarea
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  rows={fullPage ? 24 : 16}
                  placeholder={t('item.descriptionPlaceholder')}
                  className="w-full resize-y rounded-lg border border-line bg-canvas px-2.5 py-2
                             font-mono text-sm leading-relaxed focus:border-accent focus:outline-none"
                />
              )}
            </div>

            {/* Se eligen antes de crear y se suben apenas la tarea existe. Hasta entonces viven
                solo en el navegador: nada llega al servidor si el alta se cancela. */}
            <div className="space-y-1.5">
              <div className="flex items-center justify-between">
                <label className="block text-xs font-medium text-ink-muted">
                  {t('item.attachments')}
                </label>
                <button
                  type="button"
                  onClick={() => input.current?.click()}
                  className="inline-flex items-center gap-1 rounded-md px-1.5 py-0.5 text-[11px]
                             text-ink-muted hover:bg-canvas hover:text-ink"
                >
                  <Paperclip className="size-3" />
                  {t('item.attachAdd')}
                </button>
              </div>

              <input
                ref={input}
                type="file"
                multiple
                onChange={(e) => {
                  setArchivos((prev) => [...prev, ...Array.from(e.target.files ?? [])])
                  if (input.current) input.current.value = ''
                }}
                className="hidden"
              />

              {archivos.length === 0 ? (
                <p className="text-xs text-ink-subtle">{t('item.attachmentsEmpty')}</p>
              ) : (
                <ul className="space-y-1">
                  {archivos.map((f, i) => (
                    <li key={`${f.name}-${i}`} className="flex items-center gap-2 text-xs">
                      <span className="min-w-0 flex-1 truncate">{f.name}</span>
                      <button
                        type="button"
                        onClick={() => setArchivos((prev) => prev.filter((_, j) => j !== i))}
                        className="shrink-0 rounded p-0.5 text-ink-subtle hover:text-stage-blocked"
                      >
                        <X className="size-3" />
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </div>

          <div className="space-y-3">
            <div className="space-y-1.5">
              <label className="block text-xs font-medium text-ink-muted">{t('item.type')}</label>
              <select
                value={type}
                onChange={(e) => setType(e.target.value)}
                className="w-full rounded-lg border border-line bg-canvas px-2 py-1.5 text-sm
                           focus:border-accent focus:outline-none"
              >
                {types.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </div>

            <div className="space-y-1.5">
              <label className="block text-xs font-medium text-ink-muted">
                {t('board.newDifficulty')}
              </label>
              <select
                required
                value={difficulty}
                onChange={(e) => setDifficulty(e.target.value as WorkItemDifficulty)}
                className="w-full rounded-lg border border-line bg-canvas px-2 py-1.5 text-sm
                           focus:border-accent focus:outline-none"
              >
                <option value="" disabled>
                  {t('board.newDifficultyPick')}
                </option>
                {(['Baja', 'Media', 'Alta'] as const).map((level) => (
                  <option key={level} value={level}>
                    {difficultyLabel(level, labels, (l) => t(`difficulty.${l}` as const))}
                  </option>
                ))}
              </select>
            </div>

            <div className="space-y-1.5">
              <label className="block text-xs font-medium text-ink-muted">
                {t('board.newDueDate')}
              </label>
              <input
                type="date"
                value={dueDate}
                disabled={!puedeFechar}
                onChange={(e) => setDueDate(e.target.value)}
                className="w-full rounded-lg border border-line bg-canvas px-2 py-1.5 text-sm
                           focus:border-accent focus:outline-none disabled:opacity-60"
              />
              {!puedeFechar && (
                <span className="block text-[11px] text-ink-subtle">
                  {t('item.noDatePermission')}
                </span>
              )}
            </div>

            <div className="space-y-1.5">
              <label className="block text-xs font-medium text-ink-muted">
                {t('board.newEstimate')}
              </label>
              <input
                type="number"
                min="0"
                step="0.5"
                value={estimate}
                onChange={(e) => setEstimate(e.target.value)}
                placeholder="—"
                className="w-full rounded-lg border border-line bg-canvas px-2 py-1.5 text-sm
                           focus:border-accent focus:outline-none"
              />
            </div>

            {required.map((def) => (
              <div key={def.id} className="space-y-1.5">
                <label className="block text-xs font-medium text-ink-muted">
                  {def.label}
                  <span className="text-stage-blocked"> *</span>
                </label>
                <CustomFieldInput
                  def={def}
                  value={values[def.key]}
                  onChange={(value) => setValues((v) => ({ ...v, [def.key]: value }))}
                />
              </div>
            ))}
          </div>
        </div>

        {error && <p className="px-5 pb-2 text-xs text-stage-blocked">{error}</p>}

        <div className="flex items-center gap-2 border-t border-line px-5 py-3">
          <button
            type="submit"
            disabled={trabajando}
            className={cn(
              'inline-flex items-center gap-2 rounded-lg bg-accent px-3 py-1.5 text-sm',
              'font-medium text-accent-ink hover:bg-accent-hover disabled:opacity-50',
            )}
          >
            {trabajando && <Loader2 className="size-4 animate-spin" />}
            {subiendo ? t('board.uploading') : t('board.newCreate')}
          </button>

          <button
            type="button"
            onClick={onDone}
            className="rounded-lg px-3 py-1.5 text-sm text-ink-muted hover:text-ink"
          >
            {t('common.cancel')}
          </button>
        </div>
      </form>
    </div>
  )
}
