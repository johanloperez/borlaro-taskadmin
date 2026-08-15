export type StageCategory =
  | 'Backlog'
  | 'Todo'
  | 'InProgress'
  | 'Blocked'
  | 'InReview'
  | 'Done'

export type WorkItemPriority = 'Low' | 'Normal' | 'High' | 'Urgent'

export type CustomFieldType =
  | 'Text'
  | 'LongText'
  | 'Number'
  | 'Select'
  | 'MultiSelect'
  | 'Date'
  | 'Checkbox'
  | 'Url'
  | 'User'

export type ActorType = 'User' | 'Agent' | 'System'

export interface TemplateSummary {
  id: string
  key: string
  name: string
  description: string
  itemNounSingular: string
  itemNounPlural: string
  workItemTypes: string[]
  stages: { name: string; order: number; category: StageCategory; requiresBlockerReason: boolean }[]
  fields: { key: string; label: string; type: CustomFieldType; options: string[]; required: boolean }[]
}

export interface ProjectSummary {
  id: string
  key: string
  name: string
  description: string
  itemNounSingular: string
  itemNounPlural: string
  workItemTypes: string[]
  repoUrl: string | null
  openItems: number
  totalItems: number
  templateName: string | null
  isArchived: boolean
  archivedAt: string | null
  archivedByName: string | null
  /** Si lidero el proyecto: la lista ofrece archivar solo a quien puede. */
  iLead: boolean
  lastActivityAt: string | null
}

export interface Stage {
  id: string
  name: string
  order: number
  category: StageCategory
  requiresBlockerReason: boolean
  opensReviewRound: boolean
  allowedNextStageIds: string[]

  /** Quién se hace cargo del trabajo que llega a esta etapa. Al mover una tarea acá se le asigna
   *  a esta persona y se le avisa. Null = la tarea no cambia de manos, que es lo correcto para
   *  etapas de tránsito como «Bloqueado». */
  defaultAssigneeId: string | null
  defaultAssigneeName: string | null
}

export interface CustomFieldDef {
  id: string
  key: string
  label: string
  type: CustomFieldType
  options: string[]
  required: boolean
  order: number
  agentHint: string | null
}

/** Cómo participa alguien en un proyecto. No se edita: se deduce de tener trabajo asignado o
 *  revisiones a cargo. Lo único que se designa es quién lidera. */
export type Participation = 'None' | 'Reviewer' | 'Assignee' | 'Lead'

/** Lo que quien pregunta puede hacer sobre este proyecto. La UI se dibuja a partir de esto: un
 *  botón que siempre termina en 403 es peor que no tenerlo. */
export interface ProjectPermissions {
  canCreateWork: boolean
  /** Mover cualquier tarea, no solo la propia. */
  canEditAnyWork: boolean
  canAssign: boolean
  canManageProject: boolean
  myParticipation: Participation
}

export interface ProjectDetail extends Omit<ProjectSummary, 'openItems'> {
  stages: Stage[]
  customFields: CustomFieldDef[]
  permissions: ProjectPermissions
}

export interface WorkItem {
  id: string
  readableId: string
  projectKey: string
  number: number
  title: string
  descriptionMd: string
  type: string
  priority: WorkItemPriority
  stageId: string
  stageName: string
  stageCategory: StageCategory
  assigneeId: string | null
  assigneeName: string | null
  /** Quien tenia la tarea antes (para devoluciones). */
  previousAssigneeId: string | null
  previousAssigneeName?: string | null
  /** Si fue asignado por el sistema (menos carga) o manualmente. */
  assignedBySystem: boolean
  estimate: number | null
  progressPct: number
  dueDate: string | null
  sortOrder: number
  customFields: Record<string, unknown> | null
  isBlocked: boolean
  blockerReason: string | null
  /** Qué puede hacer el responsable con esta tarea. Lo decide el líder al asignarla. */
  assigneeCanMove: boolean
  assigneeCanEdit: boolean
  assigneeCanDelete: boolean
  updatedAt: string
}

export interface BoardColumn {
  stage: Stage
  items: WorkItem[]
}

export interface WorkItemEvent {
  id: string
  actorType: ActorType
  actorName: string | null
  field: string
  oldValue: string | null
  newValue: string | null
  checkInId: string | null
  createdAt: string
}

export type DeliverableKind = 'File' | 'Link'
export type ReviewStatus = 'Pending' | 'Approved' | 'ChangesRequested'

export interface ReviewRound {
  id: string
  deliverableVersionId: string
  versionNumber: number
  reviewerId: string
  reviewerName: string
  status: ReviewStatus
  commentsMd: string | null
  openedAt: string
  closedAt: string | null
}

export interface DeliverableVersion {
  id: string
  version: number
  url: string | null
  fileName: string | null
  sizeBytes: number | null
  notes: string | null
  uploadedByName: string
  createdAt: string
  isApproved: boolean
  reviews: ReviewRound[]
}

export interface Deliverable {
  id: string
  name: string
  kind: DeliverableKind
  currentVersion: number
  approvedVersionId: string | null
  reviewRoundCount: number
  versions: DeliverableVersion[]
}

export interface TeamMember {
  id: string
  name: string
  email: string
  /** Rol en la instancia. Tipado y no `string`: la UI decide qué mostrar según el rol, y con
   *  `string` cualquier typo pasa la compilación y falla en pantalla. */
  role: import('@/stores/auth').UserRole
}

export interface StageResponsible {
  userId: string
  name: string
  order: number
}

export interface WorkItemStageAssignment {
  id: string
  workItemId: string
  stageId: string
  assignedUserId: string | null
  createdAt: string
  updatedAt: string
}

/** Clase de color por familia de etapa. Se colorea por categoría y no por nombre porque el
 *  nombre cambia con la disciplina — «Montaje» y «En curso» son la misma familia. */
export const stageColor: Record<StageCategory, string> = {
  Backlog: 'text-stage-backlog',
  Todo: 'text-stage-todo',
  InProgress: 'text-stage-progress',
  Blocked: 'text-stage-blocked',
  InReview: 'text-stage-review',
  Done: 'text-stage-done',
}

export const stageDot: Record<StageCategory, string> = {
  Backlog: 'bg-stage-backlog',
  Todo: 'bg-stage-todo',
  InProgress: 'bg-stage-progress',
  Blocked: 'bg-stage-blocked',
  InReview: 'bg-stage-review',
  Done: 'bg-stage-done',
}

export const priorityLabel: Record<WorkItemPriority, string> = {
  Low: 'Baja',
  Normal: 'Normal',
  High: 'Alta',
  Urgent: 'Urgente',
}
