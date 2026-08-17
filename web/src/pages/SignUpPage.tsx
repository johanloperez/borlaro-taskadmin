import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Loader2, MailCheck } from 'lucide-react'
import { api, apiForm, ApiError } from '@/lib/api'
import { useTenancy } from '@/lib/platform'
import { useAuth } from '@/stores/auth'
import { useT } from '@/lib/i18n'
import { LanguageSwitch } from '@/components/LanguageSwitch'
import { LogoField } from '@/components/LogoField'

interface OidcInfo {
  enabled: boolean
  label: string
}

const MIN_PASSWORD = 10

/** Abrir la cuenta de una empresa.
 *
 *  Dos caminos hacia el mismo lugar. Con el proveedor de identidad es un clic: Google ya verificó
 *  quién es y solo falta el nombre de la empresa. Con email y contraseña hay una vuelta más —el
 *  correo de confirmación— porque nadie verificó nada todavía, y un formulario público que crea
 *  organizaciones sin verificar la dirección se llena de empresas fantasma. */
export function SignUpPage() {
  const t = useT()
  const tenancy = useTenancy()

  const oidc = useQuery({
    queryKey: ['oidc'],
    queryFn: () => api<OidcInfo>('/api/auth/oidc'),
    retry: false,
    staleTime: 60_000,
  })

  const [organizationName, setOrganizationName] = useState('')
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [logo, setLogo] = useState<File | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [sent, setSent] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setBusy(true)

    try {
      // Multipart: el logo (si hay) viaja en el mismo pedido que el alta.
      const form = new FormData()
      form.append('organizationName', organizationName)
      form.append('name', name)
      form.append('email', email)
      form.append('password', password)
      if (logo) form.append('logo', logo)

      await apiForm('/api/auth/registro', form)
      setSent(true)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('signUp.failed'))
    } finally {
      setBusy(false)
    }
  }

  // El registro se puede apagar, y en on-premise no existe. Mejor decirlo que mostrar un
  // formulario que el servidor va a rechazar.
  if (tenancy.data && !tenancy.data.registrationOpen) {
    return (
      <Shell title={t('signUp.closedTitle')}>
        <p className="text-sm text-ink-muted">{t('signUp.closedBody')}</p>
        <Link to="/login" className="mt-6 block text-sm text-accent hover:underline">
          {t('signUp.backToLogin')}
        </Link>
      </Shell>
    )
  }

  if (sent) {
    return (
      <Shell title={t('signUp.sentTitle')}>
        <div className="flex items-start gap-3">
          <MailCheck className="mt-0.5 size-5 shrink-0 text-accent" />
          <p className="text-sm text-ink-muted">{t('signUp.sentBody', { email })}</p>
        </div>
        <Link to="/login" className="mt-6 block text-sm text-accent hover:underline">
          {t('signUp.backToLogin')}
        </Link>
      </Shell>
    )
  }

  return (
    <Shell title={t('signUp.title')}>
      {oidc.data?.enabled && (
        <div className="mb-6">
          <button
            type="button"
            onClick={() => {
              window.location.href = '/api/auth/oidc/start'
            }}
            className="w-full rounded-lg border border-line bg-surface px-3 py-2 text-sm font-medium
                       hover:border-line-strong"
          >
            {oidc.data.label}
          </button>
          <p className="mt-1.5 text-[12px] text-ink-subtle">{t('signUp.oidcHelp')}</p>

          <div className="my-5 flex items-center gap-3">
            <span className="h-px flex-1 bg-line" />
            <span className="text-[11px] uppercase tracking-wide text-ink-subtle">
              {t('login.or')}
            </span>
            <span className="h-px flex-1 bg-line" />
          </div>
        </div>
      )}

      <form onSubmit={submit} className="space-y-4">
        <Field
          id="organizacion"
          label={t('signUp.organizationLabel')}
          value={organizationName}
          onChange={setOrganizationName}
          placeholder={t('signUp.organizationPlaceholder')}
          autoFocus
        />

        <LogoField logo={logo} onLogo={setLogo} />

        <Field
          id="nombre"
          label={t('signUp.nameLabel')}
          value={name}
          onChange={setName}
          placeholder={t('signUp.namePlaceholder')}
        />

        <Field
          id="email"
          label={t('signUp.emailLabel')}
          type="email"
          autoComplete="username"
          value={email}
          onChange={setEmail}
          placeholder={t('signUp.emailPlaceholder')}
        />

        <Field
          id="password"
          label={t('signUp.passwordLabel', { min: MIN_PASSWORD })}
          type="password"
          autoComplete="new-password"
          minLength={MIN_PASSWORD}
          value={password}
          onChange={setPassword}
        />

        {error && (
          <p role="alert" className="text-sm text-stage-blocked">
            {error}
          </p>
        )}

        <button
          type="submit"
          disabled={busy}
          className="inline-flex w-full items-center justify-center gap-2 rounded-lg bg-accent px-3
                     py-2 text-sm font-medium text-accent-ink hover:bg-accent-hover disabled:opacity-60"
        >
          {busy && <Loader2 className="size-4 animate-spin" />}
          {t('signUp.submit')}
        </button>

        <p className="text-[12px] text-ink-subtle">{t('signUp.help')}</p>
      </form>

      <Link to="/login" className="mt-6 block text-sm text-ink-muted hover:text-ink">
        {t('signUp.haveAccount')}
      </Link>

      <div className="mt-8 flex justify-center">
        <LanguageSwitch compact />
      </div>
    </Shell>
  )
}

/** La vuelta del correo: acá se crea la organización de verdad y se entra. */
export function ConfirmSignUpPage() {
  const t = useT()
  const navigate = useNavigate()
  const confirmSignUp = useAuth((s) => s.confirmSignUp)
  const [params] = useSearchParams()
  const token = params.get('token')

  const [error, setError] = useState<string | null>(null)

  // Un token es de un solo uso, y StrictMode monta cada efecto dos veces en desarrollo: sin este
  // candado el segundo intento fallaría sobre un alta que salió bien.
  const used = useRef<string | null>(null)

  useEffect(() => {
    if (!token) {
      setError(t('signUp.confirmNoToken'))
      return
    }

    if (used.current === token) return
    used.current = token

    confirmSignUp(token)
      .then(() => navigate('/', { replace: true }))
      .catch((err) =>
        setError(err instanceof ApiError ? err.message : t('signUp.confirmFailed')),
      )
  }, [token, confirmSignUp, navigate, t])

  return (
    <Shell title={error ? t('signUp.confirmFailedTitle') : t('signUp.confirmTitle')}>
      {error ? (
        <>
          <p className="text-sm text-stage-blocked">{error}</p>
          <Link to="/registro" className="mt-6 block text-sm text-accent hover:underline">
            {t('signUp.retry')}
          </Link>
        </>
      ) : (
        <p className="flex items-center gap-2 text-sm text-ink-muted">
          <Loader2 className="size-4 animate-spin" />
          {t('signUp.confirmWait')}
        </p>
      )}
    </Shell>
  )
}

function Shell({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <main className="min-h-screen grid place-items-center px-6 py-10">
      <div className="w-full max-w-sm">
        <div className="mb-8">
          <p className="text-sm text-ink-subtle">Borlaro TMS</p>
          <h1 className="mt-1 text-2xl font-semibold tracking-tight">{title}</h1>
        </div>
        {children}
      </div>
    </main>
  )
}

function Field({
  id,
  label,
  value,
  onChange,
  ...rest
}: {
  id: string
  label: string
  value: string
  onChange: (value: string) => void
} & Omit<React.InputHTMLAttributes<HTMLInputElement>, 'onChange' | 'value' | 'id'>) {
  return (
    <div className="space-y-1.5">
      <label htmlFor={id} className="block text-sm font-medium">
        {label}
      </label>
      <input
        id={id}
        required
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="w-full rounded-lg border border-line bg-surface px-3 py-2 text-sm
                   placeholder:text-ink-subtle focus:border-accent focus:outline-none"
        {...rest}
      />
    </div>
  )
}
