import { useState, type FormEvent } from 'react'
import { useParams } from 'react-router-dom'
import { CheckCircle2, Loader2 } from 'lucide-react'
import { CustomFieldInput } from '@/components/CustomFieldInput'
import { useIntakeForm, useSubmitIntake } from '@/lib/queries'
import { ApiError } from '@/lib/api'
import { useT } from '@/lib/i18n'
import { LanguageSwitch } from '@/components/LanguageSwitch'

const inputClass =
  'w-full rounded-lg border border-line bg-surface px-3 py-2 text-sm focus:border-accent focus:outline-none'

/** Página pública: no requiere sesión y no muestra nada del estado interno del proyecto —
 *  ni la cola, ni el equipo, ni las etapas. Solo lo necesario para redactar un pedido. */
export function IntakePage() {
  const t = useT()
  const { token = '' } = useParams()
  const form = useIntakeForm(token)
  const submit = useSubmitIntake(token)

  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [type, setType] = useState('')
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [fields, setFields] = useState<Record<string, unknown>>({})
  const [error, setError] = useState<string | null>(null)
  const [reference, setReference] = useState<number | null>(null)

  if (form.isLoading) {
    return <Centered>{t('common.loading')}</Centered>
  }

  // Mismo mensaje para token inválido y para formulario apagado: distinguirlos permitiría
  // sondear qué proyectos existen.
  if (form.isError || !form.data) {
    return (
      <Centered>
        <h1 className="text-lg font-semibold">{t('intake.unavailableTitle')}</h1>
        <p className="mt-1 text-sm text-ink-muted">{t('intake.unavailableBody')}</p>
      </Centered>
    )
  }

  if (reference !== null) {
    return (
      <Centered>
        <CheckCircle2 className="mx-auto size-8 text-stage-done" />
        <h1 className="mt-3 text-lg font-semibold">{t('intake.receivedTitle')}</h1>
        <p className="mt-1 text-sm text-ink-muted">
          {t('intake.receivedBody', { reference, email })}
        </p>
      </Centered>
    )
  }

  const f = form.data

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    try {
      const clean = Object.fromEntries(
        Object.entries(fields).filter(([, v]) => v !== undefined && v !== '' && v !== null),
      )
      const result = await submit.mutateAsync({
        title,
        description,
        type: type || undefined,
        submitterName: name,
        submitterEmail: email,
        customFields: clean,
      })
      setReference(result.reference)
    } catch (err) {
      setError(
        err instanceof ApiError
          ? err.status === 429
            ? t('intake.tooMany')
            : err.message
          : t('intake.failed'),
      )
    }
  }

  return (
    <main className="min-h-screen px-6 py-12">
      <div className="mx-auto w-full max-w-lg">
        <h1 className="text-xl font-semibold tracking-tight">{f.projectName}</h1>
        <p className="mt-1 text-sm text-ink-muted">
          {f.instructions ?? t('intake.defaultIntro', { noun: f.itemNounSingular })}
        </p>

        <form onSubmit={handleSubmit} className="mt-8 space-y-4">
          <Field label={t('intake.yourName')} required>
            <input required value={name} onChange={(e) => setName(e.target.value)} className={inputClass} />
          </Field>

          <Field label={t('intake.yourEmail')} required>
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className={inputClass}
              placeholder={t('intake.emailHint')}
            />
          </Field>

          <Field label={t('intake.whatTitle')} required>
            <input
              required
              maxLength={300}
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              className={inputClass}
            />
          </Field>

          <Field label={t('intake.detail')}>
            <textarea
              rows={4}
              maxLength={5000}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              className={inputClass}
              placeholder={t('intake.detailHint')}
            />
          </Field>

          {f.workItemTypes.length > 1 && (
            <Field label={t('intake.type')}>
              <select value={type} onChange={(e) => setType(e.target.value)} className={inputClass}>
                <option value="">{t('intake.unspecified')}</option>
                {f.workItemTypes.map((t) => (
                  <option key={t} value={t}>
                    {t}
                  </option>
                ))}
              </select>
            </Field>
          )}

          {f.fields.map((def) => (
            <Field key={def.id} label={def.label} required={def.required}>
              <CustomFieldInput
                def={def}
                value={fields[def.key]}
                onChange={(v) => setFields((prev) => ({ ...prev, [def.key]: v }))}
              />
            </Field>
          ))}

          {error && <p className="text-sm text-stage-blocked">{error}</p>}

          <button
            type="submit"
            disabled={submit.isPending}
            className="inline-flex w-full items-center justify-center gap-2 rounded-lg bg-accent
                       px-3 py-2.5 text-sm font-medium text-accent-ink hover:bg-accent-hover
                       disabled:opacity-50"
          >
            {submit.isPending && <Loader2 className="size-4 animate-spin" />}
            {t('intake.submit')}
          </button>
        </form>

        {/* Pública y para gente de afuera del equipo: el selector importa más acá que adentro. */}
        <div className="mt-8 flex justify-center">
          <LanguageSwitch compact />
        </div>
      </div>
    </main>
  )
}

function Field({
  label,
  required,
  children,
}: {
  label: string
  required?: boolean
  children: React.ReactNode
}) {
  return (
    <div className="space-y-1.5">
      <label className="block text-sm font-medium">
        {label}
        {required && <span className="text-stage-blocked"> *</span>}
      </label>
      {children}
    </div>
  )
}

function Centered({ children }: { children: React.ReactNode }) {
  return (
    <main className="min-h-screen grid place-items-center px-6">
      <div className="max-w-sm text-center">{children}</div>
    </main>
  )
}
