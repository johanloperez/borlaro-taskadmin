import type { CustomFieldDef } from '@/lib/types'
import { cn } from '@/lib/cn'
import { useT } from '@/lib/i18n'

const inputClass =
  'w-full rounded-lg border border-line bg-canvas px-2.5 py-1.5 text-sm focus:border-accent focus:outline-none'

/** Renderiza el control que corresponde al tipo del campo. Es lo que permite que el mismo
 *  formulario sirva a «Duración (seg)» de un proyecto de video y a «Entorno» de uno de backend
 *  sin una línea de código por disciplina. */
export function CustomFieldInput({
  def,
  value,
  onChange,
}: {
  def: CustomFieldDef
  value: unknown
  onChange: (value: unknown) => void
}) {
  const t = useT()

  switch (def.type) {
    case 'Number':
      return (
        <input
          type="number"
          className={inputClass}
          value={typeof value === 'number' ? value : ''}
          onChange={(e) => onChange(e.target.value === '' ? undefined : Number(e.target.value))}
        />
      )

    case 'Checkbox':
      return (
        <input
          type="checkbox"
          className="size-4 accent-accent"
          checked={value === true}
          onChange={(e) => onChange(e.target.checked)}
        />
      )

    case 'Date':
      return (
        <input
          type="date"
          className={inputClass}
          value={typeof value === 'string' ? value : ''}
          onChange={(e) => onChange(e.target.value || undefined)}
        />
      )

    case 'Select':
      return (
        <select
          className={inputClass}
          value={typeof value === 'string' ? value : ''}
          onChange={(e) => onChange(e.target.value || undefined)}
        >
          <option value="">—</option>
          {def.options.map((o) => (
            <option key={o} value={o}>
              {o}
            </option>
          ))}
        </select>
      )

    case 'MultiSelect': {
      const selected = Array.isArray(value) ? (value as string[]) : []
      return (
        <div className="flex flex-wrap gap-1.5">
          {def.options.map((o) => {
            const active = selected.includes(o)
            return (
              <button
                type="button"
                key={o}
                onClick={() =>
                  onChange(active ? selected.filter((s) => s !== o) : [...selected, o])
                }
                className={cn(
                  'rounded-full border px-2.5 py-1 text-xs transition-colors',
                  active
                    ? 'border-accent bg-accent-soft text-ink'
                    : 'border-line text-ink-muted hover:border-line-strong',
                )}
              >
                {o}
              </button>
            )
          })}
        </div>
      )
    }

    case 'LongText':
      return (
        <textarea
          rows={3}
          className={inputClass}
          value={typeof value === 'string' ? value : ''}
          onChange={(e) => onChange(e.target.value || undefined)}
        />
      )

    case 'Url':
      return (
        <input
          type="url"
          placeholder={t('field.urlPlaceholder')}
          className={inputClass}
          value={typeof value === 'string' ? value : ''}
          onChange={(e) => onChange(e.target.value || undefined)}
        />
      )

    default:
      return (
        <input
          type="text"
          className={inputClass}
          value={typeof value === 'string' ? value : ''}
          onChange={(e) => onChange(e.target.value || undefined)}
        />
      )
  }
}
