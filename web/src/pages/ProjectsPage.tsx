import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { Archive, ArchiveRestore, Loader2, Plus, Search, Trash2 } from 'lucide-react'
import { AppShell } from '@/components/AppShell'
import {
  useArchiveProject,
  useCreateProject,
  useDeleteProject,
  useProjects,
  useTeam,
  useTemplates,
} from '@/lib/queries'
import { canLead } from '@/lib/people'
import { LOCALE, useI18n, useT } from '@/lib/i18n'
import { useAuth } from '@/stores/auth'
import type { ProjectSummary } from '@/lib/types'
import { ApiError } from '@/lib/api'
import { stageDot } from '@/lib/types'
import { cn } from '@/lib/cn'

export function ProjectsPage() {
  const t = useT()
  const locale = LOCALE[useI18n().language]
  const [creating, setCreating] = useState(false)
  const [deleting, setDeleting] = useState<string | null>(null)
  const isAdmin = useAuth((s) => s.user?.role) === 'Admin'

  // Por defecto, los activos: un proyecto archivado es ruido salvo que lo estés buscando.
  const [estado, setEstado] = useState('activos')
  const [q, setQ] = useState('')
  const [templateId, setTemplateId] = useState('')
  const [soloMios, setSoloMios] = useState(false)

  const templates = useTemplates()
  const projects = useProjects({ estado, q, templateId, soloMios })
  const archive = useArchiveProject()

  const filtrando = q !== '' || templateId !== '' || soloMios || estado !== 'activos'
  const select = 'rounded-lg border border-line bg-surface px-2.5 py-1.5 text-sm'

  return (
    <AppShell>
      <div className="mx-auto max-w-4xl px-6 py-10">
        <div className="flex items-end justify-between">
          <div>
            <h1 className="text-xl font-semibold tracking-tight">{t('projects.title')}</h1>
            <p className="mt-1 text-sm text-ink-muted">{t('projects.subtitle')}</p>
          </div>
          <button
            onClick={() => setCreating((v) => !v)}
            className="inline-flex shrink-0 items-center gap-1.5 rounded-lg bg-accent px-3 py-1.5 text-sm
                       font-medium text-accent-ink hover:bg-accent-hover"
          >
            <Plus className="size-4" />
            {t('projects.new')}
          </button>
        </div>

        {creating && <NewProjectForm onDone={() => setCreating(false)} />}

        <div className="mt-6 flex flex-wrap items-center gap-2">
          <div className="relative">
            <Search className="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-ink-subtle" />
            <input
              value={q}
              onChange={(e) => setQ(e.target.value)}
              placeholder={t('projects.search')}
              className="w-64 rounded-lg border border-line bg-surface py-1.5 pl-8 pr-2.5 text-sm"
            />
          </div>

          <select value={estado} onChange={(e) => setEstado(e.target.value)} className={select}>
            <option value="activos">{t('projects.active')}</option>
            <option value="archivados">{t('projects.archived')}</option>
            <option value="todos">{t('projects.allStates')}</option>
          </select>

          <select value={templateId} onChange={(e) => setTemplateId(e.target.value)} className={select}>
            <option value="">{t('projects.anyDiscipline')}</option>
            {templates.data?.map((template) => (
              <option key={template.id} value={template.id}>
                {template.name}
              </option>
            ))}
          </select>

          <label className="inline-flex items-center gap-1.5 text-sm text-ink-muted">
            <input type="checkbox" checked={soloMios} onChange={(e) => setSoloMios(e.target.checked)} />
            {t('projects.mine')}
          </label>
        </div>

        <div className="mt-4 space-y-2">
          {projects.isLoading && <p className="text-sm text-ink-muted">{t('common.loading')}</p>}

          {projects.data?.length === 0 && !creating && (
            <div className="rounded-card border border-dashed border-line-strong p-8 text-center">
              <p className="text-sm text-ink-muted">
                {filtrando ? t('projects.noneMatch') : t('projects.empty')}
              </p>
            </div>
          )}

          {projects.data?.map((p) => (
            <article
              key={p.id}
              className={cn(
                'flex items-center gap-4 rounded-card border border-line bg-surface px-4 py-3',
                p.isArchived && 'opacity-70',
              )}
            >
              <Link to={`/p/${p.key}`} className="min-w-0 flex-1">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-mono text-xs text-ink-subtle">{p.key}</span>
                  <span className="truncate font-medium">{p.name}</span>
                  {p.iLead && (
                    <span className="rounded-md bg-accent-soft px-1.5 py-0.5 text-[10px] text-ink">
                      {t('projects.youLead')}
                    </span>
                  )}
                  {p.isArchived && (
                    <span className="rounded-md bg-canvas px-1.5 py-0.5 text-[10px] text-ink-muted">
                      {t('projects.archivedTag')}
                    </span>
                  )}
                </div>

                {p.description && <p className="mt-0.5 truncate text-sm text-ink-muted">{p.description}</p>}

                <p className="mt-1 text-[11px] text-ink-subtle">
                  {p.templateName}
                  {p.isArchived && p.archivedByName
                    ? t('projects.archivedBy', { name: p.archivedByName })
                    : p.lastActivityAt
                      ? t('projects.lastActivity', {
                          date: new Date(p.lastActivityAt).toLocaleDateString(locale),
                        })
                      : t('projects.noActivity')}
                </p>
              </Link>

              <span className="shrink-0 text-right text-sm text-ink-muted">
                {p.openItems} {p.openItems === 1 ? p.itemNounSingular : p.itemNounPlural}
                <span className="block text-[11px] text-ink-subtle">
                  {t('projects.ofTotal', { total: p.totalItems })}
                </span>
              </span>

              <div className="flex shrink-0 gap-1">
                {p.iLead && (
                  <button
                    onClick={() => archive.mutate({ key: p.key, undo: p.isArchived })}
                    title={p.isArchived ? t('projects.reactivate') : t('projects.archiveHint')}
                    className="rounded-md p-1.5 text-ink-muted hover:bg-canvas hover:text-ink"
                  >
                    {p.isArchived ? <ArchiveRestore className="size-4" /> : <Archive className="size-4" />}
                  </button>
                )}

                {/* Borrar es solo de admin y se lleva el tablero entero: por eso confirma
                    aparte, con el número de tareas que se va con él. */}
                {isAdmin && (
                  <button
                    onClick={() => setDeleting(p.key)}
                    title={t('projects.deleteHint')}
                    className="rounded-md p-1.5 text-ink-muted hover:bg-canvas hover:text-stage-blocked"
                  >
                    <Trash2 className="size-4" />
                  </button>
                )}
              </div>
            </article>
          ))}
        </div>
      </div>

      {deleting && (
        <DeleteProjectDialog
          project={projects.data!.find((p) => p.key === deleting)!}
          onClose={() => setDeleting(null)}
        />
      )}
    </AppShell>
  )
}

function DeleteProjectDialog({
  project,
  onClose,
}: {
  project: ProjectSummary
  onClose: () => void
}) {
  const t = useT()
  const remove = useDeleteProject()
  const [clave, setClave] = useState('')
  const [error, setError] = useState<string | null>(null)

  // Repetir la clave a mano es la única fricción que frena un borrado por inercia. El backend
  // la exige igual; acá se pide antes para no mandar una petición condenada.
  const coincide = clave.trim().toUpperCase() === project.key

  return (
    <div className="fixed inset-0 z-50 grid place-items-center bg-ink/30 p-4">
      <div className="w-full max-w-md rounded-card border border-line bg-surface p-5">
        <h2 className="text-sm font-semibold">{t('projects.deleteTitle', { name: project.name })}</h2>

        <p className="mt-2 text-sm text-ink-muted">
          {t('projects.deleteBody', {
            count: project.totalItems,
            noun: project.totalItems === 1 ? project.itemNounSingular : project.itemNounPlural,
          })}{' '}
          <strong className="text-ink">{t('projects.deleteNoUndo')}</strong>
        </p>
        <p className="mt-2 text-sm text-ink-muted">{t('projects.deleteArchiveInstead')}</p>

        <label className="mt-4 block space-y-1.5">
          <span className="text-xs font-medium text-ink-muted">
            {t('projects.deleteConfirmLabel', { key: project.key })}
          </span>
          <input
            autoFocus
            value={clave}
            onChange={(e) => setClave(e.target.value)}
            className="w-full rounded-lg border border-line bg-canvas px-3 py-2 font-mono text-sm uppercase"
          />
        </label>

        {error && <p className="mt-2 text-sm text-stage-blocked">{error}</p>}

        <div className="mt-4 flex justify-end gap-2">
          <button onClick={onClose} className="rounded-lg px-3 py-1.5 text-sm text-ink-muted">
            {t('common.cancel')}
          </button>
          <button
            onClick={async () => {
              setError(null)
              try {
                await remove.mutateAsync(project.key)
                onClose()
              } catch (err) {
                setError(err instanceof ApiError ? err.message : t('projects.deleteFailed'))
              }
            }}
            disabled={!coincide || remove.isPending}
            className="inline-flex items-center gap-2 rounded-lg bg-stage-blocked px-3 py-1.5 text-sm
                       font-medium text-white disabled:opacity-40"
          >
            {remove.isPending && <Loader2 className="size-4 animate-spin" />}
            {t('projects.deleteAction')}
          </button>
        </div>
      </div>
    </div>
  )
}

function NewProjectForm({ onDone }: { onDone: () => void }) {
  const t = useT()
  const templates = useTemplates()
  const create = useCreateProject()

  const team = useTeam()

  const [templateId, setTemplateId] = useState('')
  const [key, setKey] = useState('')
  const [name, setName] = useState('')
  const [memberIds, setMemberIds] = useState<string[]>([])
  const [labels, setLabels] = useState({ low: '', medium: '', high: '' })
  const [error, setError] = useState<string | null>(null)

  const selected = templates.data?.find((t) => t.id === templateId)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    try {
      // Quien crea el proyecto queda como líder del lado del servidor; acá se suman los demás.
      await create.mutateAsync({
        key,
        name,
        templateId,
        leadIds: memberIds,
        // Vacías es «los nombres por defecto», que se traducen al idioma de cada persona. Solo se
        // mandan si alguien escribió algo.
        difficultyLabels:
          labels.low || labels.medium || labels.high
            ? { low: labels.low || null, medium: labels.medium || null, high: labels.high || null }
            : undefined,
      })
      onDone()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('projects.createFailed'))
    }
  }

  return (
    <form
      onSubmit={handleSubmit}
      className="mt-6 rounded-card border border-line bg-surface p-5 space-y-5"
    >
      <div>
        <p className="text-sm font-medium">{t('projects.templateTitle')}</p>
        <p className="mt-0.5 text-xs text-ink-muted">{t('projects.templateHelp')}</p>

        <div className="mt-3 grid gap-2 sm:grid-cols-2">
          {templates.data?.map((template) => (
            <button
              type="button"
              key={template.id}
              onClick={() => setTemplateId(template.id)}
              className={cn(
                'rounded-lg border p-3 text-left transition-colors',
                templateId === template.id
                  ? 'border-accent bg-accent-soft/40'
                  : 'border-line hover:border-line-strong',
              )}
            >
              <span className="text-sm font-medium">{template.name}</span>
              <p className="mt-0.5 text-xs text-ink-muted">{template.description}</p>
              <div className="mt-2 flex flex-wrap items-center gap-1">
                {template.stages.slice(0, 4).map((s) => (
                  <span key={s.name} className="inline-flex items-center gap-1 text-[11px] text-ink-subtle">
                    <span className={cn('size-1.5 rounded-full', stageDot[s.category])} />
                    {s.name}
                  </span>
                ))}
                {template.stages.length > 4 && (
                  <span className="text-[11px] text-ink-subtle">+{template.stages.length - 4}</span>
                )}
              </div>
            </button>
          ))}
        </div>
      </div>

      {selected && (
        <div className="grid gap-4 sm:grid-cols-[7rem_1fr]">
          <div className="space-y-1.5">
            <label htmlFor="key" className="block text-sm font-medium">
              {t('projects.key')}
            </label>
            <input
              id="key"
              required
              maxLength={10}
              value={key}
              onChange={(e) => setKey(e.target.value.toUpperCase().replace(/[^A-Z0-9]/g, ''))}
              placeholder="VID"
              className="w-full rounded-lg border border-line bg-canvas px-3 py-2 font-mono text-sm
                         uppercase focus:border-accent focus:outline-none"
            />
            <p className="text-[11px] text-ink-subtle">
              {t('projects.keyPrefix', { key: key || 'VID' })}
            </p>
          </div>

          <div className="space-y-1.5">
            <label htmlFor="name" className="block text-sm font-medium">
              {t('projects.name')}
            </label>
            <input
              id="name"
              required
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder={t('projects.namePlaceholder')}
              className="w-full rounded-lg border border-line bg-canvas px-3 py-2 text-sm
                         focus:border-accent focus:outline-none"
            />
          </div>
        </div>
      )}

      {selected && (
        <div>
          <p className="text-sm font-medium">{t('projects.leadsTitle')}</p>
          <p className="mt-0.5 text-xs text-ink-muted">{t('projects.leadsHelp')}</p>

          <div className="mt-3 flex flex-wrap gap-2">
            {team.data?.filter((p) => canLead(p.role)).length === 0 && (
              <p className="text-sm text-stage-blocked">{t('projects.noLeads')}</p>
            )}

            {team.data?.filter((p) => canLead(p.role)).map((person) => {
              const picked = memberIds.includes(person.id)
              return (
                <button
                  type="button"
                  key={person.id}
                  onClick={() =>
                    setMemberIds((prev) =>
                      picked ? prev.filter((id) => id !== person.id) : [...prev, person.id],
                    )
                  }
                  className={cn(
                    'rounded-full border px-3 py-1 text-sm transition-colors',
                    picked
                      ? 'border-accent bg-accent-soft/50 text-ink'
                      : 'border-line text-ink-muted hover:border-line-strong',
                  )}
                >
                  {person.name}
                </button>
              )
            })}
          </div>
        </div>
      )}

      {/* Renombrar los tres niveles es opcional y va al final: la mayoría de los proyectos usa los
          nombres por defecto, y ponerlo arriba sugeriría que hay que decidirlo para poder crear.

          Se cambian las palabras, no la escala: debajo siguen siendo tres, en el mismo orden. Es
          lo que le permite al agente saber cuál extremo es cuál sin inferirlo, y lo que deja
          comparar riesgo entre proyectos que no comparten vocabulario. */}
      <div className="space-y-2 border-t border-line pt-4">
        <div>
          <p className="text-sm font-medium">{t('projects.difficultyLabels')}</p>
          <p className="text-xs text-ink-muted">{t('projects.difficultyLabelsHint')}</p>
        </div>

        <div className="grid gap-2 sm:grid-cols-3">
          {(['low', 'medium', 'high'] as const).map((slot) => (
            <label key={slot} className="text-xs text-ink-muted">
              <span className="mb-1 block">
                {slot === 'low'
                  ? t('difficulty.Baja')
                  : slot === 'medium'
                    ? t('difficulty.Media')
                    : t('difficulty.Alta')}
              </span>
              <input
                value={labels[slot]}
                onChange={(e) => setLabels((prev) => ({ ...prev, [slot]: e.target.value }))}
                placeholder={
                  slot === 'low'
                    ? t('difficulty.Baja')
                    : slot === 'medium'
                      ? t('difficulty.Media')
                      : t('difficulty.Alta')
                }
                className="w-full rounded-md border border-line bg-canvas px-2 py-1.5 text-sm
                           focus:border-accent focus:outline-none"
              />
            </label>
          ))}
        </div>
      </div>

      {error && <p className="text-sm text-stage-blocked">{error}</p>}

      <div className="flex items-center gap-2">
        <button
          type="submit"
          disabled={!templateId || create.isPending}
          className="inline-flex items-center gap-2 rounded-lg bg-accent px-3 py-1.5 text-sm
                     font-medium text-accent-ink hover:bg-accent-hover disabled:opacity-50"
        >
          {create.isPending && <Loader2 className="size-4 animate-spin" />}
          {t('projects.create')}
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
  )
}
