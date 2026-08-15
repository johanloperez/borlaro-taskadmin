import { useState } from 'react'
import { Copy, Loader2, Plus, Trash2, X } from 'lucide-react'
import { AppShell } from '@/components/AppShell'
import { ApiError } from '@/lib/api'
import { cn } from '@/lib/cn'
import { useT, type Translate } from '@/lib/i18n'
import { stageDot, type CustomFieldType, type StageCategory } from '@/lib/types'
import {
  EMPTY_TEMPLATE,
  useDeleteTemplate,
  useDuplicateTemplate,
  useSaveTemplate,
  useTemplatesFull,
  type TemplateDetail,
  type TemplateInput,
} from '@/lib/templates'

/** Categorías y tipos guardan claves, no textos: el valor que viaja al servidor es el mismo en
 *  los tres idiomas, y lo que cambia es cómo se lee. */
const CATEGORIES = [
  { value: 'Backlog', label: 'stage.backlog', help: 'stage.backlogHelp' },
  { value: 'Todo', label: 'stage.todo', help: 'stage.todoHelp' },
  { value: 'InProgress', label: 'stage.inProgress', help: 'stage.inProgressHelp' },
  { value: 'Blocked', label: 'stage.blocked', help: 'stage.blockedHelp' },
  { value: 'InReview', label: 'stage.inReview', help: 'stage.inReviewHelp' },
  { value: 'Done', label: 'stage.done', help: 'stage.doneHelp' },
] as const satisfies readonly {
  value: StageCategory
  label: Parameters<Translate>[0]
  help: Parameters<Translate>[0]
}[]

const FIELD_TYPES = [
  { value: 'Text', label: 'fieldType.text' },
  { value: 'LongText', label: 'fieldType.longText' },
  { value: 'Number', label: 'fieldType.number' },
  { value: 'Select', label: 'fieldType.select' },
  { value: 'MultiSelect', label: 'fieldType.multiSelect' },
  { value: 'Date', label: 'fieldType.date' },
  { value: 'Checkbox', label: 'fieldType.checkbox' },
  { value: 'Url', label: 'fieldType.url' },
  { value: 'User', label: 'fieldType.user' },
] as const satisfies readonly { value: CustomFieldType; label: Parameters<Translate>[0] }[]

/** ABM de plantillas de disciplina. Es el mecanismo que hace que el mismo sistema sirva a un
 *  equipo de video y a uno de backend: acá se definen las etapas, las transiciones válidas, los
 *  campos, el vocabulario y el contexto que el agente usa para hablar el idioma del oficio. */
export function TemplatesPage() {
  const t = useT()
  const templates = useTemplatesFull()
  const duplicate = useDuplicateTemplate()
  const remove = useDeleteTemplate()

  const [editing, setEditing] = useState<{ id?: string; input: TemplateInput } | null>(null)
  const [error, setError] = useState<string | null>(null)

  function edit(template: TemplateDetail) {
    setError(null)
    setEditing({
      id: template.id,
      input: {
        key: template.key,
        name: template.name,
        description: template.description,
        itemNounSingular: template.itemNounSingular,
        itemNounPlural: template.itemNounPlural,
        workItemTypes: template.workItemTypes,
        agentContext: template.agentContext,
        stages: template.stages,
        fields: template.fields,
      },
    })
  }

  function run(promise: Promise<unknown>) {
    setError(null)
    promise.catch((err) => setError(err instanceof ApiError ? err.message : t('templates.failed')))
  }

  return (
    <AppShell>
      <div className="mx-auto max-w-4xl px-6 py-10">
        <div className="flex items-end justify-between">
          <div>
            <h1 className="text-xl font-semibold tracking-tight">{t('templates.title')}</h1>
            <p className="mt-1 text-sm text-ink-muted">{t('templates.subtitle')}</p>
          </div>
          <button
            onClick={() => {
              setError(null)
              setEditing({ input: structuredClone(EMPTY_TEMPLATE) })
            }}
            className="inline-flex shrink-0 items-center gap-1.5 rounded-lg bg-accent px-3 py-1.5
                       text-sm font-medium text-accent-ink hover:bg-accent-hover"
          >
            <Plus className="size-4" />
            {t('templates.new')}
          </button>
        </div>

        {error && <p className="mt-4 text-sm text-stage-blocked">{error}</p>}

        <div className="mt-8 space-y-2">
          {templates.isLoading && <p className="text-sm text-ink-muted">{t('common.loading')}</p>}

          {templates.data?.map((template) => (
            <article
              key={template.id}
              className="flex items-start justify-between gap-4 rounded-card border border-line
                         bg-surface px-4 py-3"
            >
              <button onClick={() => edit(template)} className="min-w-0 flex-1 text-left">
                <div className="flex items-center gap-2">
                  <span className="font-medium">{template.name}</span>
                  <span className="font-mono text-[11px] text-ink-subtle">{template.key}</span>
                  {template.isBuiltIn && (
                    <span className="rounded-md bg-canvas px-1.5 py-0.5 text-[11px] text-ink-muted">
                      {t('templates.builtIn')}
                    </span>
                  )}
                </div>
                <p className="mt-0.5 truncate text-sm text-ink-muted">{template.description}</p>
                <div className="mt-2 flex flex-wrap items-center gap-1.5">
                  {template.stages.map((s) => (
                    <span key={s.name} className="inline-flex items-center gap-1 text-[11px] text-ink-subtle">
                      <span className={cn('size-1.5 rounded-full', stageDot[s.category])} />
                      {s.name}
                    </span>
                  ))}
                </div>
                <p className="mt-2 text-[11px] text-ink-subtle">
                  {t('templates.counts', {
                    fields: template.fields.length,
                    projects: template.projectsUsing,
                  })}
                </p>
              </button>

              <div className="flex shrink-0 gap-1">
                <button
                  onClick={() => run(duplicate.mutateAsync(template.id))}
                  title={t('templates.duplicate')}
                  className="rounded-md p-1.5 text-ink-muted hover:bg-canvas hover:text-ink"
                >
                  <Copy className="size-4" />
                </button>
                {/* El botón se dibuja siempre, aunque no se pueda usar: un botón ausente no
                    explica nada, y «no encuentro cómo borrar esto» es peor que «no se puede
                    borrar, y acá dice por qué». */}
                <button
                  onClick={() => run(remove.mutateAsync(template.id))}
                  disabled={template.isBuiltIn}
                  title={
                    template.isBuiltIn
                      ? t('templates.deleteBuiltIn')
                      : t('templates.delete', { name: template.name })
                  }
                  className="rounded-md p-1.5 text-ink-muted hover:bg-canvas hover:text-stage-blocked
                             disabled:cursor-not-allowed disabled:opacity-30 disabled:hover:text-ink-muted"
                >
                  <Trash2 className="size-4" />
                </button>
              </div>
            </article>
          ))}
        </div>
      </div>

      {editing && (
        <Editor
          id={editing.id}
          initial={editing.input}
          onClose={() => setEditing(null)}
        />
      )}
    </AppShell>
  )
}

function Editor({
  id,
  initial,
  onClose,
}: {
  id?: string
  initial: TemplateInput
  onClose: () => void
}) {
  const t = useT()
  const save = useSaveTemplate()
  const [draft, setDraft] = useState<TemplateInput>(initial)
  const [error, setError] = useState<string | null>(null)

  const set = <K extends keyof TemplateInput>(key: K, value: TemplateInput[K]) =>
    setDraft((d) => ({ ...d, [key]: value }))

  function renameStage(index: number, name: string) {
    setDraft((d) => {
      const before = d.stages[index].name
      return {
        ...d,
        stages: d.stages.map((s, i) => ({
          ...s,
          name: i === index ? name : s.name,
          // Renombrar arrastra las transiciones que apuntaban al nombre viejo: si no, la
          // plantilla queda apuntando a una etapa que ya no existe y el guardado la rechaza.
          allowedNext: s.allowedNext.map((n) => (n === before ? name : n)),
        })),
      }
    })
  }

  function removeStage(index: number) {
    setDraft((d) => {
      const gone = d.stages[index].name
      return {
        ...d,
        stages: d.stages
          .filter((_, i) => i !== index)
          .map((s) => ({ ...s, allowedNext: s.allowedNext.filter((n) => n !== gone) })),
      }
    })
  }

  async function submit() {
    setError(null)
    try {
      await save.mutateAsync({ id, ...draft })
      onClose()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('templates.saveFailed'))
    }
  }

  const input = 'w-full rounded-lg border border-line bg-canvas px-3 py-1.5 text-sm focus:border-accent'

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-ink/20 p-4">
      <div className="my-8 w-full max-w-2xl rounded-card border border-line bg-surface shadow-lg">
        <header className="flex items-center justify-between border-b border-line px-5 py-3">
          <h2 className="text-sm font-medium">
            {id ? t('templates.editTitle') : t('templates.newTitle')}
          </h2>
          <button onClick={onClose} className="rounded-md p-1 text-ink-muted hover:bg-canvas">
            <X className="size-4" />
          </button>
        </header>

        <div className="space-y-6 p-5">
          <div className="grid gap-4 sm:grid-cols-[8rem_1fr]">
            <label className="space-y-1.5">
              <span className="block text-sm font-medium">{t('templates.key')}</span>
              <input
                value={draft.key}
                onChange={(e) => set('key', e.target.value)}
                placeholder={t('templates.keyPlaceholder')}
                className={cn(input, 'font-mono')}
              />
            </label>
            <label className="space-y-1.5">
              <span className="block text-sm font-medium">{t('templates.name')}</span>
              <input
                value={draft.name}
                onChange={(e) => set('name', e.target.value)}
                placeholder={t('templates.namePlaceholder')}
                className={input}
              />
            </label>
          </div>

          <label className="block space-y-1.5">
            <span className="block text-sm font-medium">{t('templates.description')}</span>
            <input
              value={draft.description}
              onChange={(e) => set('description', e.target.value)}
              className={input}
            />
          </label>

          <div className="grid gap-4 sm:grid-cols-2">
            <label className="space-y-1.5">
              <span className="block text-sm font-medium">{t('templates.nounSingular')}</span>
              <input
                value={draft.itemNounSingular}
                onChange={(e) => set('itemNounSingular', e.target.value)}
                placeholder={t('templates.nounSingularPlaceholder')}
                className={input}
              />
            </label>
            <label className="space-y-1.5">
              <span className="block text-sm font-medium">{t('templates.nounPlural')}</span>
              <input
                value={draft.itemNounPlural}
                onChange={(e) => set('itemNounPlural', e.target.value)}
                placeholder={t('templates.nounPluralPlaceholder')}
                className={input}
              />
            </label>
          </div>

          <label className="block space-y-1.5">
            <span className="block text-sm font-medium">{t('templates.workTypes')}</span>
            <input
              value={draft.workItemTypes.join(', ')}
              onChange={(e) => set('workItemTypes', e.target.value.split(',').map((t) => t.trim()))}
              placeholder={t('templates.workTypesPlaceholder')}
              className={input}
            />
            <span className="block text-[11px] text-ink-subtle">{t('templates.commaSeparated')}</span>
          </label>

          {/* ── Etapas ── */}
          <section>
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-medium">{t('templates.stagesTitle')}</h3>
              <button
                onClick={() =>
                  setDraft((d) => ({
                    ...d,
                    stages: [
                      ...d.stages,
                      {
                        name: t('templates.stageDefault', { n: d.stages.length + 1 }),
                        order: d.stages.length,
                        category: 'InProgress',
                        requiresBlockerReason: false,
                        opensReviewRound: false,
                        allowedNext: [],
                      },
                    ],
                  }))
                }
                className="text-sm text-accent"
              >
                {t('templates.addStage')}
              </button>
            </div>

            <p className="mt-1 text-xs text-ink-muted">
              {t('templates.stagesHelp')}
            </p>

            <div className="mt-3 space-y-3">
              {draft.stages.map((stage, index) => (
                <div key={index} className="rounded-lg border border-line p-3">
                  <div className="flex items-center gap-2">
                    <input
                      value={stage.name}
                      onChange={(e) => renameStage(index, e.target.value)}
                      className={cn(input, 'flex-1')}
                    />
                    <select
                      value={stage.category}
                      onChange={(e) =>
                        setDraft((d) => ({
                          ...d,
                          stages: d.stages.map((s, i) =>
                            i === index ? { ...s, category: e.target.value as StageCategory } : s,
                          ),
                        }))
                      }
                      className={cn(input, 'w-40')}
                    >
                      {CATEGORIES.map((c) => (
                        <option key={c.value} value={c.value}>
                          {t(c.label)}
                        </option>
                      ))}
                    </select>
                    <button
                      onClick={() => removeStage(index)}
                      className="rounded-md p-1.5 text-ink-muted hover:text-stage-blocked"
                      title={t('templates.removeStage')}
                    >
                      <Trash2 className="size-4" />
                    </button>
                  </div>

                  <div className="mt-2 flex flex-wrap gap-3 text-xs">
                    <label className="flex items-center gap-1.5">
                      <input
                        type="checkbox"
                        checked={stage.requiresBlockerReason}
                        onChange={(e) =>
                          setDraft((d) => ({
                            ...d,
                            stages: d.stages.map((s, i) =>
                              i === index ? { ...s, requiresBlockerReason: e.target.checked } : s,
                            ),
                          }))
                        }
                      />
                      {t('templates.asksReason')}
                    </label>
                    <label className="flex items-center gap-1.5">
                      <input
                        type="checkbox"
                        checked={stage.opensReviewRound}
                        onChange={(e) =>
                          setDraft((d) => ({
                            ...d,
                            stages: d.stages.map((s, i) =>
                              i === index ? { ...s, opensReviewRound: e.target.checked } : s,
                            ),
                          }))
                        }
                      />
                      {t('templates.opensReview')}
                    </label>
                  </div>

                  <div className="mt-2">
                    <p className="text-[11px] text-ink-subtle">{t('templates.canMoveTo')}</p>
                    <div className="mt-1 flex flex-wrap gap-1.5">
                      {draft.stages
                        .filter((_, i) => i !== index)
                        .map((other) => {
                          const on = stage.allowedNext.includes(other.name)
                          return (
                            <button
                              key={other.name}
                              onClick={() =>
                                setDraft((d) => ({
                                  ...d,
                                  stages: d.stages.map((s, i) =>
                                    i === index
                                      ? {
                                          ...s,
                                          allowedNext: on
                                            ? s.allowedNext.filter((n) => n !== other.name)
                                            : [...s.allowedNext, other.name],
                                        }
                                      : s,
                                  ),
                                }))
                              }
                              className={cn(
                                'rounded-full border px-2 py-0.5 text-[11px]',
                                on
                                  ? 'border-accent bg-accent-soft/50 text-ink'
                                  : 'border-line text-ink-muted',
                              )}
                            >
                              {other.name}
                            </button>
                          )
                        })}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </section>

          {/* ── Campos ── */}
          <section>
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-medium">Campos personalizados</h3>
              <button
                onClick={() =>
                  setDraft((d) => ({
                    ...d,
                    fields: [
                      ...d.fields,
                      { key: '', label: '', type: 'Text', options: [], required: false, agentHint: null },
                    ],
                  }))
                }
                className="text-sm text-accent"
              >
                {t('templates.addField')}
              </button>
            </div>

            <div className="mt-3 space-y-3">
              {draft.fields.map((field, index) => (
                <div key={index} className="rounded-lg border border-line p-3 space-y-2">
                  <div className="flex gap-2">
                    <input
                      value={field.key}
                      onChange={(e) =>
                        setDraft((d) => ({
                          ...d,
                          fields: d.fields.map((f, i) => (i === index ? { ...f, key: e.target.value } : f)),
                        }))
                      }
                      placeholder={t('templates.fieldKey')}
                      className={cn(input, 'w-40 font-mono')}
                    />
                    <input
                      value={field.label}
                      onChange={(e) =>
                        setDraft((d) => ({
                          ...d,
                          fields: d.fields.map((f, i) => (i === index ? { ...f, label: e.target.value } : f)),
                        }))
                      }
                      placeholder={t('templates.fieldLabel')}
                      className={cn(input, 'flex-1')}
                    />
                    <select
                      value={field.type}
                      onChange={(e) =>
                        setDraft((d) => ({
                          ...d,
                          fields: d.fields.map((f, i) =>
                            i === index ? { ...f, type: e.target.value as CustomFieldType } : f,
                          ),
                        }))
                      }
                      className={cn(input, 'w-36')}
                    >
                      {FIELD_TYPES.map((option) => (
                        <option key={option.value} value={option.value}>
                          {t(option.label)}
                        </option>
                      ))}
                    </select>
                    <button
                      onClick={() =>
                        setDraft((d) => ({ ...d, fields: d.fields.filter((_, i) => i !== index) }))
                      }
                      className="rounded-md p-1.5 text-ink-muted hover:text-stage-blocked"
                    >
                      <Trash2 className="size-4" />
                    </button>
                  </div>

                  {(field.type === 'Select' || field.type === 'MultiSelect') && (
                    <input
                      value={field.options.join(', ')}
                      onChange={(e) =>
                        setDraft((d) => ({
                          ...d,
                          fields: d.fields.map((f, i) =>
                            i === index ? { ...f, options: e.target.value.split(',').map((o) => o.trim()) } : f,
                          ),
                        }))
                      }
                      placeholder={t('templates.fieldOptions')}
                      className={input}
                    />
                  )}

                  <input
                    value={field.agentHint ?? ''}
                    onChange={(e) =>
                      setDraft((d) => ({
                        ...d,
                        fields: d.fields.map((f, i) =>
                          i === index ? { ...f, agentHint: e.target.value || null } : f,
                        ),
                      }))
                    }
                    placeholder={t('templates.fieldHint')}
                    className={input}
                  />

                  <label className="flex items-center gap-1.5 text-xs">
                    <input
                      type="checkbox"
                      checked={field.required}
                      onChange={(e) =>
                        setDraft((d) => ({
                          ...d,
                          fields: d.fields.map((f, i) =>
                            i === index ? { ...f, required: e.target.checked } : f,
                          ),
                        }))
                      }
                    />
                    {t('templates.fieldRequired')}
                  </label>
                </div>
              ))}
            </div>
          </section>

          <label className="block space-y-1.5">
            <span className="block text-sm font-medium">{t('templates.agentContext')}</span>
            <textarea
              value={draft.agentContext}
              onChange={(e) => set('agentContext', e.target.value)}
              rows={4}
              placeholder={t('templates.agentContextPlaceholder')}
              className={cn(input, 'resize-y')}
            />
            <span className="block text-[11px] text-ink-subtle">
              {t('templates.agentContextHelp')}
            </span>
          </label>

          {error && <p className="text-sm text-stage-blocked">{error}</p>}
        </div>

        <footer className="flex items-center justify-end gap-2 border-t border-line px-5 py-3">
          <button onClick={onClose} className="rounded-lg px-3 py-1.5 text-sm text-ink-muted">
            {t('common.cancel')}
          </button>
          <button
            onClick={submit}
            disabled={save.isPending}
            className="inline-flex items-center gap-2 rounded-lg bg-accent px-3 py-1.5 text-sm
                       font-medium text-accent-ink disabled:opacity-50"
          >
            {save.isPending && <Loader2 className="size-4 animate-spin" />}
            {t('common.save')}
          </button>
        </footer>
      </div>
    </div>
  )
}
