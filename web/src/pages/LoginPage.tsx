import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { useAuth, type PendingRegistration } from '@/stores/auth'
import { api, ApiError } from '@/lib/api'
import { useTenancy } from '@/lib/platform'
import { useT, type Translate } from '@/lib/i18n'
import { LanguageSwitch } from '@/components/LanguageSwitch'
import { LogoField } from '@/components/LogoField'

interface OidcInfo {
  enabled: boolean
  label: string
}

/** Los códigos que puede dejar el callback en la URL. El servidor no manda el texto del error:
 *  escribir en la página lo que devuelva un proveedor externo es abrirle una puerta a cualquiera
 *  que controle uno. Manda un código de esta lista y acá se traduce. */
function errorMessage(t: Translate, code: string, email: string | null): string {
  const who = email ? `«${email}»` : t('login.error.thatAccount')

  switch (code) {
    case 'sin_cuenta':
      return t('login.error.noAccount', { who })
    case 'inactivo':
      return t('login.error.inactive', { who })
    case 'dominio':
      return t('login.error.domain', { who })
    case 'email':
      return t('login.error.email')
    case 'ya_vinculado':
      return t('login.error.alreadyLinked', { who })
    case 'varias_organizaciones':
      return t('login.error.manyOrganizations', { who })
    case 'estado':
      return t('login.error.state')
    case 'config':
      return t('login.error.config')
    default:
      return t('login.error.generic')
  }
}

export function LoginPage() {
  const t = useT()
  const navigate = useNavigate()
  const login = useAuth((s) => s.login)
  const loginWithTicket = useAuth((s) => s.loginWithTicket)
  const [params, setParams] = useSearchParams()

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  // Un alta a medio camino: el proveedor ya dijo quién es y falta el nombre de la organización.
  const [pending, setPending] = useState<PendingRegistration | null>(null)

  // Si no hay proveedor configurado, la pantalla queda exactamente como estaba: un formulario y
  // nada más. El botón no aparece «apagado», no aparece.
  const oidc = useQuery({
    queryKey: ['oidc'],
    queryFn: () => api<OidcInfo>('/api/auth/oidc'),
    retry: false,
    staleTime: 60_000,
  })

  // Para saber si esta instalación admite empresas nuevas. En on-premise no, y entonces el texto
  // no promete algo que el servidor va a rechazar.
  const tenancy = useTenancy()

  const ticket = params.get('ticket')
  const errorCode = params.get('error')
  const volver = params.get('volver') ?? '/'

  // El ticket es de un solo uso, y en desarrollo StrictMode monta cada efecto dos veces: sin este
  // candado el segundo intento canjearía un ticket ya gastado y pintaría un error sobre un login
  // que salió bien.
  const redeemed = useRef<string | null>(null)

  // La vuelta del proveedor deja el ticket en la URL. Se canjea acá, se limpia la URL —para que
  // un F5 no reintente un ticket ya gastado— y se entra.
  useEffect(() => {
    if (!ticket || redeemed.current === ticket) return
    redeemed.current = ticket

    setBusy(true)
    setParams({}, { replace: true })

    loginWithTicket(ticket)
      .then((registration) => {
        // Sin cuenta y con el registro abierto: en vez de entrar, se le pregunta cómo se llama su
        // organización. El ticket sigue vivo hasta que la cree.
        if (registration) setPending(registration)
        else navigate(volver, { replace: true })
      })
      .catch(() => setError(t('login.ticketExpired')))
      .finally(() => setBusy(false))
  }, [ticket, volver, loginWithTicket, navigate, setParams, t])

  useEffect(() => {
    if (!errorCode) return
    setError(errorMessage(t, errorCode, params.get('email')))
    setParams({}, { replace: true })
  }, [errorCode, params, setParams, t])

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setBusy(true)
    try {
      await login(email, password)
      navigate('/', { replace: true })
    } catch (err) {
      setError(
        err instanceof ApiError && err.status === 401
          ? t('login.badCredentials')
          : t('login.noServer'),
      )
    } finally {
      setBusy(false)
    }
  }

  if (pending) {
    return <CreateOrganization pending={pending} onCancel={() => setPending(null)} />
  }

  return (
    <main className="min-h-screen grid place-items-center px-6">
      <div className="w-full max-w-sm">
        <div className="mb-8">
          <h1 className="text-2xl font-semibold tracking-tight">TaskAdmin</h1>
          <p className="mt-1 text-sm text-ink-muted">
            {tenancy.data?.registrationOpen ? t('login.subtitleWithSignUp') : t('login.subtitle')}
          </p>
        </div>

        {oidc.data?.enabled && (
          <div className="mb-6">
            <button
              type="button"
              disabled={busy}
              onClick={() => {
                // Navegación completa, no fetch: el servidor responde con un redirect al
                // proveedor, y ese redirect tiene que verlo el navegador.
                window.location.href = `/api/auth/oidc/start?volver=${encodeURIComponent(volver)}`
              }}
              className="w-full rounded-lg border border-line bg-surface px-3 py-2 text-sm font-medium
                         hover:border-line-strong disabled:opacity-60"
            >
              {oidc.data.label}
            </button>

            <div className="my-5 flex items-center gap-3">
              <span className="h-px flex-1 bg-line" />
              <span className="text-[11px] uppercase tracking-wide text-ink-subtle">
                {t('login.or')}
              </span>
              <span className="h-px flex-1 bg-line" />
            </div>
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <label htmlFor="email" className="block text-sm font-medium">
              {t('common.email')}
            </label>
            <input
              id="email"
              type="email"
              autoComplete="username"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="w-full rounded-lg border border-line bg-surface px-3 py-2 text-sm
                         placeholder:text-ink-subtle focus:border-accent focus:outline-none"
              placeholder={t('login.emailPlaceholder')}
            />
          </div>

          <div className="space-y-1.5">
            <label htmlFor="password" className="block text-sm font-medium">
              {t('common.password')}
            </label>
            <input
              id="password"
              type="password"
              autoComplete="current-password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full rounded-lg border border-line bg-surface px-3 py-2 text-sm
                         focus:border-accent focus:outline-none"
            />
          </div>

          {error && (
            <p role="alert" className="text-sm text-stage-blocked">
              {error}
            </p>
          )}

          <button
            type="submit"
            disabled={busy}
            className="w-full rounded-lg bg-accent px-3 py-2 text-sm font-medium text-accent-ink
                       hover:bg-accent-hover disabled:opacity-60 inline-flex items-center justify-center gap-2"
          >
            {busy && <Loader2 className="size-4 animate-spin" />}
            {t('login.submit')}
          </button>
        </form>

        {/* El alta tiene que verse. El mecanismo ya existía —entrar con el proveedor sin tener
            cuenta abre una organización nueva— pero sin este bloque nadie se enteraba: había que
            adivinar que el botón de entrar también servía para registrarse. Un camino que existe
            y no se ve es un camino que no existe. */}
        {tenancy.data?.registrationOpen && (
          <div className="mt-8 rounded-card border border-line bg-surface px-4 py-3">
            <p className="text-sm font-medium">{t('login.signUpTitle')}</p>
            <p className="mt-0.5 text-[13px] text-ink-muted">{t('login.signUpBody')}</p>

            <Link
              to="/registro"
              className="mt-3 block rounded-lg border border-accent px-3 py-2 text-center text-sm
                         font-medium text-accent hover:bg-accent-soft"
            >
              {t('login.signUpAction')}
            </Link>
          </div>
        )}

        {/* El selector va en la pantalla de entrada y no solo adentro de la app: quien todavía no
            tiene sesión también necesita leer esta página, y es la primera que ve. */}
        <div className="mt-8 flex justify-center">
          <LanguageSwitch compact />
        </div>
      </div>
    </main>
  )
}

/** El segundo paso del alta. Una sola pregunta: cómo se llama la empresa.
 *
 *  Todo lo demás —quién es, su email, su nombre— ya lo dijo el proveedor y no se vuelve a pedir.
 *  Un formulario de registro largo acá sería pedirle a alguien que reescriba lo que Google acaba
 *  de confirmar. */
function CreateOrganization({
  pending,
  onCancel,
}: {
  pending: PendingRegistration
  onCancel: () => void
}) {
  const t = useT()
  const navigate = useNavigate()
  const registerOrganization = useAuth((s) => s.registerOrganization)

  const [name, setName] = useState('')
  const [logo, setLogo] = useState<File | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setBusy(true)

    try {
      await registerOrganization(pending.ticket, name, logo)
      navigate('/', { replace: true })
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('signUpOidc.failed'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <main className="min-h-screen grid place-items-center px-6">
      <div className="w-full max-w-sm">
        <div className="mb-8">
          <h1 className="text-2xl font-semibold tracking-tight">{t('signUpOidc.title')}</h1>
          <p className="mt-1 text-sm text-ink-muted">
            {t('signUpOidc.subtitle', { email: pending.email })}
          </p>
        </div>

        <form onSubmit={submit} className="space-y-4">
          <div className="space-y-1.5">
            <label htmlFor="organizacion" className="block text-sm font-medium">
              {t('signUpOidc.nameLabel')}
            </label>
            <input
              id="organizacion"
              required
              autoFocus
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder={t('signUpOidc.namePlaceholder')}
              className="w-full rounded-lg border border-line bg-surface px-3 py-2 text-sm
                         placeholder:text-ink-subtle focus:border-accent focus:outline-none"
            />
            <p className="text-[12px] text-ink-subtle">{t('signUpOidc.help')}</p>
          </div>

          <LogoField logo={logo} onLogo={setLogo} />

          {error && (
            <p role="alert" className="text-sm text-stage-blocked">
              {error}
            </p>
          )}

          <button
            type="submit"
            disabled={busy}
            className="w-full rounded-lg bg-accent px-3 py-2 text-sm font-medium text-accent-ink
                       hover:bg-accent-hover disabled:opacity-60 inline-flex items-center justify-center gap-2"
          >
            {busy && <Loader2 className="size-4 animate-spin" />}
            {t('signUpOidc.submit')}
          </button>

          <button
            type="button"
            onClick={onCancel}
            className="w-full rounded-lg border border-line px-3 py-2 text-sm text-ink-muted"
          >
            {t('common.cancel')}
          </button>
        </form>
      </div>
    </main>
  )
}
