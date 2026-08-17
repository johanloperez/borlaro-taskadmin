import { useMemo, useRef, useState, type FormEvent } from 'react'
import { useParams, useSearchParams } from 'react-router-dom'
import {
  DndContext,
  DragOverlay,
  PointerSensor,
  useDraggable,
  useDroppable,
  useSensor,
  useSensors,
  type DragEndEvent,
  type DragStartEvent,
} from '@dnd-kit/core'
import { AlertTriangle, Loader2, Maximize2, Plus, Share2, UserCog, UserPlus, X } from 'lucide-react'
import { AppShell } from '@/components/AppShell'
import { CustomFieldInput } from '@/components/CustomFieldInput'
import { ItemDetailPanel } from '@/components/ItemDetailPanel'
import { TeamAside, TeamButton } from '@/components/TeamPanel'
import { initials } from '@/lib/people'
import {
  useBoard,
  useCreateItem,
  useEnableIntake,
  useProject,
  useStageResponsibles,
  useStageResponsibleMutation,
  useTeam,
  useTransitionItem,
} from '@/lib/queries'
import { ApiError } from '@/lib/api'
import { CreateItemDialog } from '@/components/CreateItemDialog'
import { useBoardRealtime } from '@/lib/realtime'
import {
  difficultyLabel,
  priorityLabel,
  stageDot,
  type CustomFieldDef,
  type DifficultyLabels,
  type Stage,
  type WorkItem,
  type WorkItemDifficulty,
} from '@/lib/types'
import { cn } from '@/lib/cn'
import { useT } from '@/lib/i18n'
import { useAuth } from '@/stores/auth'

export function BoardPage() {
  const t = useT()
  const { key = '' } = useParams()
  const project = useProject(key)
  const board = useBoard(key)
  const transition = useTransitionItem(key)

  // El tablero se actualiza solo: si otra persona —o el agente— mueve algo, aparece acá sin que
  // nadie refresque.
  useBoardRealtime(key)

  const myId = useAuth((s) => s.user?.id)

  // Quién puede mover qué: el líder mueve todo, el resto solo lo suyo. Se decide acá y se pasa
  // a cada tarjeta, porque el backend responde 403 y arrastrar para que te lo rechacen es una
  // forma cara de enterarse.
  const canMove = (item: WorkItem) =>
    project.data?.permissions.canEditAnyWork === true ||
    (item.assigneeId !== null && item.assigneeId === myId && item.assigneeCanMove)

  const [dragging, setDragging] = useState<WorkItem | null>(null)

  // `?item=` abre una tarea directamente. Es lo que hace que un aviso del agente —«VID-31 lleva
  // 4 días bloqueada»— lleve a la tarea y no al tablero a buscarla.
  const [searchParams, setSearchParams] = useSearchParams()
  const [selectedId, setSelectedId] = useState<string | null>(searchParams.get('item'))
  const [notice, setNotice] = useState<string | null>(null)
  const [blockerPrompt, setBlockerPrompt] = useState<{ item: WorkItem; stage: Stage } | null>(null)
  const [fullCreate, setFullCreate] = useState(false)
  const [adding, setAdding] = useState(false)

  const sensors = useSensors(
    // 6px de holgura: sin esto, un click en la tarjeta se interpreta como arrastre y nunca
    // se llega a abrir el detalle.
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
  )

  const selected = useMemo(
    () => board.data?.flatMap((c) => c.items).find((i) => i.id === selectedId) ?? null,
    [board.data, selectedId],
  )

  // Etapas a las que el item que se está arrastrando puede ir. `null` significa "sin
  // restricción declarada", que el backend trata como etapa libre.
  const reachableStageIds = useMemo(() => {
    if (!dragging || !board.data) return null
    const current = board.data.find((c) => c.stage.id === dragging.stageId)?.stage
    if (!current || current.allowedNextStageIds.length === 0) return null
    return new Set([...current.allowedNextStageIds, current.id])
  }, [dragging, board.data])

  function handleDragStart(event: DragStartEvent) {
    setDragging((event.active.data.current as { item: WorkItem } | undefined)?.item ?? null)
  }

  async function handleDragEnd(event: DragEndEvent) {
    const item = dragging
    setDragging(null)
    if (!item || !event.over) return

    const stage = event.over.data.current as Stage | undefined
    if (!stage || stage.id === item.stageId) return

    // La UI no ofrece movimientos que el backend va a rechazar. Igual el backend valida:
    // esto es cortesía con el usuario, no la defensa.
    if (stage.requiresBlockerReason) {
      setBlockerPrompt({ item, stage })
      return
    }

    try {
      setNotice(null)
      await transition.mutateAsync({ id: item.id, toStageId: stage.id })
    } catch (err) {
      setNotice(err instanceof ApiError ? err.message : 'No se pudo mover.')
    }
  }

  if (project.isLoading || board.isLoading) {
    return (
      <AppShell>
        <div className="p-10 text-sm text-ink-muted">Cargando tablero…</div>
      </AppShell>
    )
  }

  if (!project.data) {
    return (
      <AppShell>
        <div className="p-10 text-sm text-ink-muted">No se encontró el proyecto «{key}».</div>
      </AppShell>
    )
  }

  const p = project.data
  const openCount = board.data?.reduce((n, c) => n + c.items.length, 0) ?? 0

  return (
    <AppShell
      aside={
        <>
          <TeamButton projectKey={p.key} canManage={p.permissions.canManageProject} />

          {/* El formulario público es del líder: es una puerta anónima de escritura sobre este
              tablero. */}
          {p.permissions.canManageProject && <IntakeButton projectKey={p.key} />}

          {p.permissions.canCreateWork && (
            <button
              onClick={() => setAdding(true)}
              className="inline-flex items-center gap-1.5 rounded-lg bg-accent px-2.5 py-1.5 text-sm
                         font-medium text-accent-ink hover:bg-accent-hover"
            >
              <Plus className="size-4" />
              {/* Un verbo y no "Nuevo/Nueva": el sustantivo lo define la plantilla y puede ser de
                  cualquier género, incluso en plantillas que cree el usuario. Un adjetivo no
                  concuerda, y menos todavía al traducirlo. */}
              {t('board.create', { noun: p.itemNounSingular })}
            </button>
          )}
        </>
      }
    >
      <div className="flex h-[calc(100vh-3.5rem)]">
        {/* Panel del proyecto: de qué va, quién lo integra y con qué rol. Antes había que
            abrir un diálogo para saber quién estaba en el equipo. */}
        <aside className="hidden w-64 shrink-0 space-y-5 overflow-y-auto border-r border-line
                          px-4 py-4 lg:block">
          <div>
            <div className="flex items-center gap-2">
              <span className="font-mono text-xs text-ink-subtle">{p.key}</span>
              {p.permissions.myParticipation === 'Lead' && (
                <span className="rounded-md bg-accent-soft px-1.5 py-0.5 text-[10px] text-ink">
                  {t('board.youLead')}
                </span>
              )}
            </div>
            <h2 className="mt-1 text-sm font-medium">{p.name}</h2>
            {p.description && <p className="mt-1 text-xs text-ink-muted">{p.description}</p>}
            <p className="mt-2 text-xs text-ink-subtle">
              {t('board.openCount', {
                count: openCount,
                noun: openCount === 1 ? p.itemNounSingular : p.itemNounPlural,
              })}
            </p>
          </div>

          <TeamAside projectKey={p.key} />

          {p.customFields.length > 0 && (
            <section>
              <h3 className="text-[11px] font-medium uppercase tracking-wide text-ink-subtle">
                {t('board.projectFields')}
              </h3>
              <ul className="mt-2 space-y-1">
                {p.customFields.map((f) => (
                  <li key={f.id} className="text-xs text-ink-muted">
                    {f.label}
                    {f.required && <span className="text-stage-blocked"> *</span>}
                  </li>
                ))}
              </ul>
            </section>
          )}

          <p className="rounded-lg border border-dashed border-line-strong p-2 text-[11px] text-ink-muted">
            {p.permissions.canEditAnyWork
              ? t('board.roleLead')
              : p.permissions.myParticipation === 'Assignee'
                ? t('board.roleAssignee')
                : p.permissions.myParticipation === 'Reviewer'
                  ? t('board.roleReviewer')
                  : t('board.roleReader')}
          </p>
        </aside>

        <div className="flex-1 min-w-0 flex flex-col">
          <div className="flex items-center gap-3 px-5 py-3 border-b border-line lg:hidden">
            <span className="font-mono text-xs text-ink-subtle">{p.key}</span>
            <h1 className="text-sm font-medium">{p.name}</h1>
            <span className="text-xs text-ink-subtle">
              {t('board.openCount', {
                count: openCount,
                noun: openCount === 1 ? p.itemNounSingular : p.itemNounPlural,
              })}
            </span>
          </div>

          {notice && (
            <div className="mx-5 mt-3 flex items-start gap-2 rounded-lg border border-stage-blocked/30
                            bg-stage-blocked/5 px-3 py-2 text-sm">
              <AlertTriangle className="mt-0.5 size-4 shrink-0 text-stage-blocked" />
              <span className="flex-1">{notice}</span>
              <button onClick={() => setNotice(null)} className="text-ink-muted hover:text-ink">
                <X className="size-4" />
              </button>
            </div>
          )}

          {adding && (
            <NewItemForm
              onExpand={() => {
                setAdding(false)
                setFullCreate(true)
              }}
              projectKey={p.key}
              types={p.workItemTypes}
              fields={p.customFields}
              labels={p.difficultyLabels}
              onDone={() => setAdding(false)}
            />
          )}

          <DndContext sensors={sensors} onDragStart={handleDragStart} onDragEnd={handleDragEnd}>
            <div className="flex-1 overflow-x-auto">
              <div className="flex h-full gap-3 p-5">
                {board.data?.map((column) => (
                  <Column
                    key={column.stage.id}
                    stage={column.stage}
                    items={column.items}
                    isDragActive={dragging !== null}
                    reachable={reachableStageIds === null || reachableStageIds.has(column.stage.id)}
                    selectedId={selectedId}
                    canMove={canMove}
                    canAssign={p.permissions.canAssign}
                    projectKey={key}
                    onSelect={setSelectedId}
                  />
                ))}
              </div>
            </div>

            <DragOverlay>
              {dragging && <Card item={dragging} dragging />}
            </DragOverlay>
          </DndContext>
        </div>

        {selected && (
          <ItemDetailPanel
            item={selected}
            project={p}
            onClose={() => {
              setSelectedId(null)
              // El parámetro se limpia al cerrar: si quedara, recargar la página volvería a
              // abrir una tarea que la persona ya cerró.
              if (searchParams.has('item')) {
                searchParams.delete('item')
                setSearchParams(searchParams, { replace: true })
              }
            }}
          />
        )}
      </div>

      {fullCreate && (
        <CreateItemDialog
          projectKey={p.key}
          types={p.workItemTypes}
          fields={p.customFields}
          labels={p.difficultyLabels}
          onDone={() => setFullCreate(false)}
          onCreated={(id) => {
            setFullCreate(false)
            setSelectedId(id)
          }}
        />
      )}

      {blockerPrompt && (
        <BlockerDialog
          stageName={blockerPrompt.stage.name}
          pending={transition.isPending}
          onCancel={() => setBlockerPrompt(null)}
          onConfirm={async (reason) => {
            try {
              await transition.mutateAsync({
                id: blockerPrompt.item.id,
                toStageId: blockerPrompt.stage.id,
                blockerReason: reason,
              })
              setBlockerPrompt(null)
            } catch (err) {
              setNotice(err instanceof ApiError ? err.message : 'No se pudo mover.')
              setBlockerPrompt(null)
            }
          }}
        />
      )}
    </AppShell>
  )
}

/** Genera el enlace público de intake y lo copia. El token no se muestra en el tablero de
 *  forma permanente: es un secreto, y verlo en pantalla invita a compartir capturas. */
function IntakeButton({ projectKey }: { projectKey: string }) {
  const t = useT()
  const enable = useEnableIntake(projectKey)
  const [copied, setCopied] = useState(false)

  return (
    <button
      onClick={async () => {
        const result = await enable.mutateAsync({})
        const url = `${window.location.origin}/intake/${result.token}`
        try {
          await navigator.clipboard.writeText(url)
          setCopied(true)
          setTimeout(() => setCopied(false), 3000)
        } catch {
          window.prompt(t('board.intakePrompt'), url)
        }
      }}
      disabled={enable.isPending}
      title={t('board.intakeTitle')}
      className="inline-flex items-center gap-1.5 rounded-lg border border-line px-2.5 py-1.5
                 text-sm text-ink-muted hover:text-ink disabled:opacity-50"
    >
      <Share2 className="size-4" />
      {copied ? t('board.intakeCopied') : t('board.intakeLink')}
    </button>
  )
}

/** Quién se hace cargo del trabajo que llega a esta etapa.
 *
 *  Vive en el encabezado de la columna y no en una pantalla de configuración aparte, porque es
 *  donde la pregunta aparece: se ve el tablero, se ve que «Edición» no tiene a nadie, y se
 *  resuelve ahí. Escondido en un formulario de ajustes, nadie lo completaría.
 *
 *  Quien no puede repartir trabajo lo ve pero no lo edita — enterarse de a quién le va a llegar
 *  lo que uno termina no es un privilegio de administración. */
function StageOwner({
  stage,
  projectKey,
  canAssign,
}: {
  stage: Stage
  projectKey: string
  canAssign: boolean
}) {
  const t = useT()
  const [open, setOpen] = useState(false)
  // `/api/auth/users` ya devuelve solo la gente activa de la organización: el servidor no ofrece
  // como candidato a alguien dado de baja, así que no hace falta filtrarlo acá.
  const people = useTeam()

  // La lista solo se pide cuando se abre el desplegable: son tantas consultas como columnas tenga
  // el tablero, y ninguna hace falta hasta que alguien quiere repartir.
  const responsibles = useStageResponsibles(projectKey, stage.id, open)
  const { add, remove } = useStageResponsibleMutation(projectKey, stage.id)

  const enLista = responsibles.data ?? []

  // Quién atiende esta etapa, para el resumen de la columna. La lista manda; el titular único
  // aparece solo si la etapa se configuró antes de que la lista existiera (§12).
  const resumen =
    enLista.length > 0
      ? enLista.length === 1
        ? enLista[0].name
        : t('board.stageOwnerCount', { count: String(enLista.length) })
      : stage.defaultAssigneeName

  if (!canAssign) {
    if (!resumen) return null
    return (
      <p className="mt-1.5 truncate text-[11px] text-ink-subtle" title={resumen}>
        {t('board.stageOwner', { name: resumen })}
      </p>
    )
  }

  return (
    <div className="relative mt-1.5">
      <button
        onClick={() => setOpen((v) => !v)}
        className={cn(
          'inline-flex max-w-full items-center gap-1 truncate rounded px-1 py-0.5 text-[11px]',
          'hover:bg-canvas',
          resumen ? 'text-ink-subtle' : 'text-ink-subtle/70 italic',
        )}
      >
        <UserCog className="size-3 shrink-0" />
        <span className="truncate">
          {resumen ? t('board.stageOwner', { name: resumen }) : t('board.stageOwnerNone')}
        </span>
      </button>

      {open && (
        <div
          className="absolute left-0 top-full z-20 mt-1 w-64 rounded-lg border border-line
                     bg-surface p-1 shadow-lg"
        >
          <p className="px-2 py-1.5 text-[11px] leading-snug text-ink-muted">
            {t('board.stageOwnerHelp')}
          </p>

          {/* Varias personas pueden atender la misma etapa: cuando llega una tarea se le da a la
              que tenga menos trabajo abierto. Con una sola en la lista se comporta igual que el
              titular único de antes, así que no hay dos formas de configurar lo mismo. */}
          <div className="max-h-56 overflow-y-auto">
            {people.data?.map((person) => {
              const puesto = enLista.some((r) => r.userId === person.id)
              return (
                <button
                  key={person.id}
                  disabled={add.isPending || remove.isPending}
                  onClick={() => (puesto ? remove.mutate(person.id) : add.mutate(person.id))}
                  className={cn(
                    'flex w-full items-center gap-2 truncate rounded px-2 py-1.5 text-left text-sm',
                    'hover:bg-canvas disabled:opacity-50',
                    puesto && 'font-medium text-ink',
                  )}
                >
                  <span
                    className={cn(
                      'inline-block size-3 shrink-0 rounded-sm border',
                      puesto ? 'border-accent bg-accent' : 'border-line',
                    )}
                  />
                  <span className="truncate">{person.name}</span>
                </button>
              )
            })}
          </div>

          {enLista.length === 0 && stage.defaultAssigneeName && (
            <p className="border-t border-line px-2 py-1.5 text-[11px] leading-snug text-ink-subtle">
              {t('board.stageOwnerLegacy', { name: stage.defaultAssigneeName })}
            </p>
          )}
        </div>
      )}
    </div>
  )
}

function Column({
  stage,
  items,
  isDragActive,
  reachable,
  selectedId,
  canMove,
  canAssign,
  projectKey,
  onSelect,
}: {
  stage: Stage
  items: WorkItem[]
  isDragActive: boolean
  reachable: boolean
  selectedId: string | null
  canMove: (item: WorkItem) => boolean
  canAssign: boolean
  projectKey: string
  onSelect: (id: string) => void
}) {
  const { setNodeRef, isOver } = useDroppable({ id: stage.id, data: stage })

  return (
    <div
      ref={setNodeRef}
      className={cn(
        'flex w-72 shrink-0 flex-col rounded-card border transition-colors',
        isOver && reachable ? 'border-accent bg-accent-soft/20' : 'border-line bg-surface',
        // Mientras se arrastra, atenuar las columnas inalcanzables: el usuario ve el grafo de
        // transiciones sin tener que descubrirlo a fuerza de rechazos.
        isDragActive && !reachable && 'opacity-40',
      )}
    >
      <div className="border-b border-line px-3 py-2.5">
        <div className="flex items-center gap-2">
          <span className={cn('size-2 rounded-full', stageDot[stage.category])} />
          <span className="text-sm font-medium truncate">{stage.name}</span>
          <span className="ml-auto text-xs text-ink-subtle">{items.length}</span>
        </div>

        <StageOwner stage={stage} projectKey={projectKey} canAssign={canAssign} />
      </div>

      <div className="flex-1 space-y-2 overflow-y-auto p-2">
        {items.map((item) => (
          <DraggableCard
            key={item.id}
            item={item}
            selected={item.id === selectedId}
            canMove={canMove(item)}
            canAssign={canAssign}
            onSelect={() => onSelect(item.id)}
          />
        ))}
      </div>
    </div>
  )
}

function DraggableCard({
  item,
  selected,
  canMove,
  canAssign,
  onSelect,
}: {
  item: WorkItem
  selected: boolean
  /** Falso cuando la tarea es de otra persona y no lidero el proyecto: se puede abrir para
   *  mirarla, pero no arrastrar. Mejor que dejar arrastrar y devolver un 403 al soltar. */
  canMove: boolean
  canAssign: boolean
  onSelect: () => void
}) {
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: item.id,
    data: { item },
    disabled: !canMove,
  })

  // Se abre el detalle midiendo cuánto se movió el puntero entre down y up, con el mismo
  // umbral (6px) que activa el arrastre, en vez de con onClick. Así el click residual que el
  // navegador emite al soltar tras arrastrar no abre el panel por accidente, y el gesto se
  // comporta igual con mouse que con touch.
  const start = useRef<{ x: number; y: number } | null>(null)

  return (
    <div
      ref={setNodeRef}
      {...attributes}
      {...listeners}
      onPointerDown={(event) => {
        start.current = { x: event.clientX, y: event.clientY }
        listeners?.onPointerDown?.(event)
      }}
      onPointerUp={(event) => {
        const from = start.current
        start.current = null
        if (from && Math.hypot(event.clientX - from.x, event.clientY - from.y) < 6) {
          onSelect()
        }
      }}
      className={cn(isDragging && 'opacity-30')}
    >
      <Card item={item} selected={selected} readOnly={!canMove} canAssign={canAssign} />
    </div>
  )
}

function Card({
  item,
  selected,
  dragging,
  readOnly,
  canAssign,
}: {
  item: WorkItem
  selected?: boolean
  dragging?: boolean
  readOnly?: boolean
  /** Si quien mira puede repartir trabajo. Cambia «sin asignar» de estado a acción. */
  canAssign?: boolean
}) {
  const t = useT()

  return (
    <div
      className={cn(
        'rounded-lg border bg-surface-raised p-2.5',
        readOnly ? 'cursor-pointer' : 'cursor-grab active:cursor-grabbing',
        selected ? 'border-accent' : 'border-line',
        dragging && 'shadow-lg rotate-1',
      )}
      title={readOnly ? t('board.readOnlyCard') : undefined}
    >
      <div className="flex items-center gap-2">
        <span className="font-mono text-[11px] text-ink-subtle">{item.readableId}</span>
        {item.priority !== 'Normal' && (
          <span
            className={cn(
              'rounded px-1.5 py-0.5 text-[10px] font-medium',
              item.priority === 'Urgent' || item.priority === 'High'
                ? 'bg-stage-blocked/10 text-stage-blocked'
                : 'bg-canvas text-ink-subtle',
            )}
          >
            {priorityLabel[item.priority]}
          </span>
        )}
        {item.isBlocked && <AlertTriangle className="size-3 text-stage-blocked" />}
      </div>

      <p className="mt-1 text-sm leading-snug">{item.title}</p>

      <div className="mt-2 flex items-center gap-2 text-[11px] text-ink-subtle">
        <span>{item.type}</span>
        {item.dueDate ? (
          <span>· {item.dueDate}</span>
        ) : (
          /* Sin fecha y ya en curso es trabajo que nadie sabe cuándo llega. No se impide —bloquear
             el movimiento frenaría a quien no puede poner fechas— pero se ve, y quien lidera
             recibe además el aviso en Novedades. */
          item.stageCategory === 'InProgress' && (
            <span className="text-stage-blocked">· {t('board.noDate')}</span>
          )
        )}
        {item.progressPct > 0 && <span>· {item.progressPct}%</span>}

        {/* Quién la tiene, siempre visible en la tarjeta. Sin asignar es una señal en sí misma,
            así que también se dibuja: un tablero lleno de tareas sin dueño se ve de un vistazo.
            Y si quien mira puede asignar, se dibuja como acción y no como estado: «sin asignar»
            en gris no le dice a nadie que la asignación está a un clic. */}
        <span className="ml-auto flex items-center gap-1">
          {item.assigneeName ? (
            <>
              <span
                className="grid size-5 place-items-center rounded-full bg-canvas text-[9px]
                           font-medium text-ink-muted ring-1 ring-line"
                title={item.assigneeName}
              >
                {initials(item.assigneeName)}
              </span>
              <span className="max-w-24 truncate">{item.assigneeName}</span>
            </>
          ) : canAssign ? (
            <span
              className="inline-flex items-center gap-1 rounded-full border border-dashed border-accent/60
                         px-1.5 py-0.5 text-accent"
              title={t('board.assignHint')}
            >
              <UserPlus className="size-3" />
              {t('board.assign')}
            </span>
          ) : (
            <span className="rounded px-1 text-ink-subtle/70 ring-1 ring-dashed ring-line">
              {t('board.unassigned')}
            </span>
          )}
        </span>
      </div>
    </div>
  )
}

function BlockerDialog({
  stageName,
  pending,
  onCancel,
  onConfirm,
}: {
  stageName: string
  pending: boolean
  onCancel: () => void
  onConfirm: (reason: string) => void
}) {
  const t = useT()
  const [reason, setReason] = useState('')

  return (
    <div className="fixed inset-0 z-50 grid place-items-center bg-black/40 px-6">
      <div className="w-full max-w-md rounded-card border border-line bg-surface p-5">
        <h2 className="text-sm font-semibold">{t('board.moveTo', { stage: stageName })}</h2>
        <p className="mt-1 text-sm text-ink-muted">{t('board.blockerHelp')}</p>

        <textarea
          autoFocus
          rows={3}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          placeholder={t('board.blockerPlaceholder')}
          className="mt-3 w-full rounded-lg border border-line bg-canvas px-3 py-2 text-sm
                     focus:border-accent focus:outline-none"
        />

        <div className="mt-4 flex justify-end gap-2">
          <button onClick={onCancel} className="rounded-lg px-3 py-1.5 text-sm text-ink-muted hover:text-ink">
            {t('common.cancel')}
          </button>
          <button
            onClick={() => onConfirm(reason)}
            disabled={!reason.trim() || pending}
            className="inline-flex items-center gap-2 rounded-lg bg-accent px-3 py-1.5 text-sm
                       font-medium text-accent-ink hover:bg-accent-hover disabled:opacity-50"
          >
            {pending && <Loader2 className="size-4 animate-spin" />}
            {t('board.move')}
          </button>
        </div>
      </div>
    </div>
  )
}

function NewItemForm({
  projectKey,
  types,
  fields,
  labels,
  onDone,
  onExpand,
}: {
  projectKey: string
  types: string[]
  fields: CustomFieldDef[]
  labels: DifficultyLabels
  onDone: () => void
  /** Pasar al alta completa, con descripción y adjuntos. Lo que ya se escribió no se arrastra:
   *  el título de dos palabras del alta rápida rara vez es el que uno quiere en un brief. */
  onExpand: () => void
}) {
  const t = useT()
  const create = useCreateItem(projectKey)
  const [title, setTitle] = useState('')
  const [type, setType] = useState(types[0] ?? 'Tarea')
  const [dueDate, setDueDate] = useState('')
  const [estimate, setEstimate] = useState('')
  const [difficulty, setDifficulty] = useState<'' | WorkItemDifficulty>('')
  const [values, setValues] = useState<Record<string, unknown>>({})
  const [error, setError] = useState<string | null>(null)

  // Solo los obligatorios. El resto se completa después desde el detalle: este formulario es para
  // anotar rápido algo que hay que hacer, y meterle diez campos lo convierte en un trámite.
  //
  // Los obligatorios sí van, porque sin ellos el servidor rechaza la creación — y antes de esto
  // el rechazo llegaba como «entorno es obligatorio» sin que hubiera ningún lugar donde ponerlo.
  const required = fields.filter((f) => f.required)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    try {
      await create.mutateAsync({
        title,
        type,
        dueDate: dueDate || null,
        // Vacío es «no lo sé todavía», que no es lo mismo que cero: cero horas estimadas diría
        // que la tarea no cuesta nada, y eso después aparece como una subestimación gigante.
        estimate: estimate === '' ? null : Number(estimate),
        difficulty: difficulty as WorkItemDifficulty,
        customFields: required.length > 0 ? values : undefined,
      })
      onDone()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('board.createFailed'))
    }
  }

  return (
    <form
      onSubmit={handleSubmit}
      className="mx-5 mt-3 rounded-lg border border-line bg-surface p-2"
    >
      <div className="flex items-center gap-2">
      <input
        autoFocus
        required
        value={title}
        onChange={(e) => setTitle(e.target.value)}
        placeholder={t('board.newTitle')}
        className="flex-1 rounded-md border border-line bg-canvas px-2.5 py-1.5 text-sm
                   focus:border-accent focus:outline-none"
      />
      <select
        value={type}
        onChange={(e) => setType(e.target.value)}
        className="rounded-md border border-line bg-canvas px-2 py-1.5 text-sm focus:border-accent focus:outline-none"
      >
        {types.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
      <button
        type="submit"
        disabled={create.isPending}
        className="rounded-md bg-accent px-3 py-1.5 text-sm font-medium text-accent-ink
                   hover:bg-accent-hover disabled:opacity-50"
      >
        {t('board.newCreate')}
      </button>
      <button
        type="button"
        onClick={onExpand}
        title={t('board.expandCreate')}
        className="rounded-md px-2 py-1.5 text-sm text-ink-muted hover:text-ink"
      >
        <Maximize2 className="size-4" />
      </button>

      <button type="button" onClick={onDone} className="rounded-md px-2 py-1.5 text-sm text-ink-muted">
        <X className="size-4" />
      </button>
      </div>

      {/* Fecha, horas y dificultad van acá y no escondidas en el detalle: son lo que decide el
          comportamiento del agente, y un campo que hay que ir a buscar después no se completa.
          Los tres son opcionales, así que anotar algo rápido sigue siendo escribir y Enter. */}
      <div className="mt-2 flex flex-wrap items-end gap-2 border-t border-line pt-2">
        <label className="text-xs text-ink-muted">
          <span className="mb-1 block">{t('board.newDueDate')}</span>
          <input
            type="date"
            value={dueDate}
            onChange={(e) => setDueDate(e.target.value)}
            className="rounded-md border border-line bg-canvas px-2 py-1.5 text-sm
                       focus:border-accent focus:outline-none"
          />
        </label>

        <label className="text-xs text-ink-muted">
          <span className="mb-1 block">{t('board.newEstimate')}</span>
          <input
            type="number"
            min="0"
            step="0.5"
            value={estimate}
            onChange={(e) => setEstimate(e.target.value)}
            placeholder="—"
            className="w-24 rounded-md border border-line bg-canvas px-2 py-1.5 text-sm
                       focus:border-accent focus:outline-none"
          />
        </label>

        <label className="text-xs text-ink-muted">
          <span className="mb-1 block">{t('board.newDifficulty')}</span>
          {/* Arranca vacío y es obligatorio, no preseleccionado. Un valor puesto de fábrica se
              acepta sin mirarlo, y entonces el nivel pasa a significar «lo que salió» en vez de
              «lo que alguien juzgó» — y sobre eso el agente no puede modular nada. Prefiere
              costar un clic a costar el dato. */}
          <select
            required
            value={difficulty}
            onChange={(e) => setDifficulty(e.target.value as WorkItemDifficulty)}
            className="rounded-md border border-line bg-canvas px-2 py-1.5 text-sm
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
        </label>
      </div>

      {required.length > 0 && (
        <div className="mt-2 grid gap-2 border-t border-line pt-2 sm:grid-cols-2">
          {required.map((def) => (
            <label key={def.id} className="text-xs text-ink-muted">
              <span className="mb-1 block">
                {def.label}
                <span className="text-stage-blocked"> *</span>
              </span>
              <CustomFieldInput
                def={def}
                value={values[def.key]}
                onChange={(value) => setValues((v) => ({ ...v, [def.key]: value }))}
              />
            </label>
          ))}
        </div>
      )}

      {error && <p className="mt-2 text-xs text-stage-blocked">{error}</p>}
    </form>
  )
}
