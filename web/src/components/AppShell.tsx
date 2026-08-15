import { Link, useLocation } from 'react-router-dom'
import type { ReactNode } from 'react'
import {
  BookOpen,
  Building2,
  History,
  LayoutGrid,
  LogOut,
  Mail,
  MessageSquare,
  Shapes,
  ShieldCheck,
  SlidersHorizontal,
  Users,
} from 'lucide-react'
import { useTodayCheckIn } from '@/lib/agent'
import { useUnreadMessages } from '@/lib/messages'
import { useAuth } from '@/stores/auth'
import { cn } from '@/lib/cn'
import { useT } from '@/lib/i18n'
import { LanguageSwitch } from '@/components/LanguageSwitch'
import { OrgLogo } from '@/components/OrgLogo'

export function AppShell({ children, aside }: { children: ReactNode; aside?: ReactNode }) {
  const t = useT()
  const user = useAuth((s) => s.user)
  const logout = useAuth((s) => s.logout)
  const { pathname } = useLocation()
  const today = useTodayCheckIn()
  const unread = useUnreadMessages().data?.count ?? 0

  const isOperator = user?.role === 'PlatformOperator'
  const isAdmin = user?.role === 'Admin'
  const canApprove = isAdmin || user?.role === 'Manager'

  // El operador no ve el resto de la navegación porque no tendría qué mostrarle: no pertenece a
  // ninguna empresa y todas esas pantallas le saldrían vacías. Ofrecerle enlaces que no llevan a
  // nada haría parecer roto lo que en realidad es el aislamiento funcionando.
  if (isOperator) {
    return (
      <div className="min-h-screen flex flex-col">
        <header className="border-b border-line bg-surface">
          <div className="flex items-center justify-between px-5 h-14">
            <div className="flex items-center gap-6">
              {/* Acá sí manda el nombre de la plataforma: el operador no pertenece a ninguna
                  empresa, así que no hay logo de organización que poner en su lugar. */}
              <Link to="/plataforma" className="text-base font-semibold tracking-tight">
                TaskAdmin
              </Link>
              <nav className="flex items-center gap-1 text-sm">
                <Link
                  to="/plataforma"
                  className={cn(
                    'inline-flex items-center gap-1.5 rounded-md px-2.5 py-1.5 hover:bg-canvas',
                    pathname === '/plataforma' ? 'text-ink font-medium' : 'text-ink-muted',
                  )}
                >
                  <Building2 className="size-4" />
                  {t('nav.organizations')}
                </Link>

                <Link
                  to="/configuracion"
                  className={cn(
                    'inline-flex items-center gap-1.5 rounded-md px-2.5 py-1.5 hover:bg-canvas',
                    pathname === '/configuracion' ? 'text-ink font-medium' : 'text-ink-muted',
                  )}
                >
                  <SlidersHorizontal className="size-4" />
                  {t('nav.installation')}
                </Link>
              </nav>
            </div>

            <div className="flex items-center gap-3 text-sm">
              <LanguageSwitch compact />
              <span className="rounded-md bg-canvas px-1.5 py-0.5 text-[11px] text-ink-subtle">
                {t('nav.operatorBadge')}
              </span>
              <span className="text-ink-muted">{user?.name}</span>
              <button
                onClick={logout}
                title={t('nav.signOut')}
                className="rounded-md p-1.5 text-ink-muted hover:bg-canvas hover:text-ink"
              >
                <LogOut className="size-4" />
              </button>
            </div>
          </div>
        </header>

        <div className="flex-1 min-h-0">{children}</div>
      </div>
    )
  }

  return (
    <div className="min-h-screen flex flex-col">
      <header className="border-b border-line bg-surface">
        <div className="flex items-center justify-between px-5 h-14">
          <div className="flex items-center gap-6">
            {/* La marca del encabezado es la empresa de quien entró, no la de la plataforma.
                Quien usa esto todos los días trabaja para su empresa; el nombre del software es
                un dato de quien lo instaló y vive chico a la derecha. */}
            <Link
              to="/"
              title={user?.organizationName ?? undefined}
              className="flex min-w-0 items-center gap-2.5"
            >
              {user?.organizationName ? (
                <>
                  <OrgLogo
                    organizationId={user.organizationId}
                    hasLogo={Boolean(user.logoPath)}
                    name={user.organizationName}
                    className="size-9 shrink-0 rounded-md object-contain"
                  />
                  {/* El nombre se esconde en pantallas angostas y queda el logo: la barra tiene
                      que seguir entrando con la navegación al lado. */}
                  <span className="hidden max-w-52 truncate text-base font-semibold tracking-tight sm:block">
                    {user.organizationName}
                  </span>
                </>
              ) : (
                <span className="text-base font-semibold tracking-tight">TaskAdmin</span>
              )}
            </Link>

            <nav className="flex items-center gap-1 text-sm">
              <Link
                to="/"
                className={cn(
                  'inline-flex items-center gap-1.5 rounded-md px-2.5 py-1.5 hover:bg-canvas',
                  pathname === '/' ? 'text-ink font-medium' : 'text-ink-muted',
                )}
              >
                <LayoutGrid className="size-4" />
                {t('nav.projects')}
              </Link>

              {/* La actividad la ve cualquiera, acotada a sus propios proyectos: saber quién
                  movió tu tarea no es un privilegio de administración. */}
              <Link
                to="/actividad"
                className={cn(
                  'inline-flex items-center gap-1.5 rounded-md px-2.5 py-1.5 hover:bg-canvas',
                  pathname === '/actividad' ? 'text-ink font-medium' : 'text-ink-muted',
                )}
              >
                <History className="size-4" />
                {t('nav.activity')}
              </Link>

              <Link
                to="/mensajes"
                className={cn(
                  'inline-flex items-center gap-1.5 rounded-md px-2.5 py-1.5 hover:bg-canvas',
                  pathname.startsWith('/mensajes') ? 'text-ink font-medium' : 'text-ink-muted',
                )}
              >
                <Mail className="size-4" />
                {t('nav.messages')}
                {unread > 0 && (
                  <span className="rounded-full bg-accent px-1.5 text-[11px] text-accent-ink">
                    {unread}
                  </span>
                )}
              </Link>

              {isAdmin && (
                <Link
                  to="/personas"
                  className={cn(
                    'inline-flex items-center gap-1.5 rounded-md px-2.5 py-1.5 hover:bg-canvas',
                    pathname === '/personas' ? 'text-ink font-medium' : 'text-ink-muted',
                  )}
                >
                  <Users className="size-4" />
                  {t('nav.people')}
                </Link>
              )}

              {isAdmin && (
                <Link
                  to="/plantillas"
                  className={cn(
                    'inline-flex items-center gap-1.5 rounded-md px-2.5 py-1.5 hover:bg-canvas',
                    pathname === '/plantillas' ? 'text-ink font-medium' : 'text-ink-muted',
                  )}
                >
                  <Shapes className="size-4" />
                  {t('nav.templates')}
                </Link>
              )}

              {isAdmin && (
                <Link
                  to="/configuracion"
                  className={cn(
                    'inline-flex items-center gap-1.5 rounded-md px-2.5 py-1.5 hover:bg-canvas',
                    pathname === '/configuracion' ? 'text-ink font-medium' : 'text-ink-muted',
                  )}
                >
                  <SlidersHorizontal className="size-4" />
                  {t('nav.settings')}
                </Link>
              )}

              {canApprove && (
                <Link
                  to="/aprobaciones"
                  className={cn(
                    'inline-flex items-center gap-1.5 rounded-md px-2.5 py-1.5 hover:bg-canvas',
                    pathname === '/aprobaciones' ? 'text-ink font-medium' : 'text-ink-muted',
                  )}
                >
                  <ShieldCheck className="size-4" />
                  {t('nav.approvals')}
                </Link>
              )}

              {/* El manual es una página suelta, no una pantalla de la aplicación: se sirve
                  estática desde /manual y se abre en otra pestaña, para no sacar a nadie de lo
                  que estaba haciendo por ir a consultar algo. Por eso un <a> y no un <Link>. */}
              <a
                href="/manual"
                target="_blank"
                rel="noopener"
                className="inline-flex items-center gap-1.5 rounded-md px-2.5 py-1.5 text-ink-muted hover:bg-canvas"
              >
                <BookOpen className="size-4" />
                {t('nav.manual')}
              </a>
            </nav>
          </div>

          <div className="flex items-center gap-3 text-sm">
            {aside}

            <LanguageSwitch compact />

            {/* El check-in pendiente se ve desde cualquier pantalla: si hay que ir a buscarlo,
                no se hace. */}
            {today.data && (
              <Link
                to={`/checkin/${today.data.id}`}
                className="inline-flex items-center gap-1.5 rounded-md bg-accent-soft px-2.5 py-1.5 text-ink"
              >
                <MessageSquare className="size-4" />
                {t('nav.todayCheckIn')}
              </Link>
            )}

            <span className="text-ink-muted">{user?.name}</span>

            {/* La marca de la plataforma, chica y sin enlace: es un dato de quién provee el
                software, no un lugar al que ir. El encabezado lo ocupa la empresa. */}
            <span className="hidden border-l border-line pl-3 text-[11px] tracking-wide text-ink-subtle lg:inline">
              TaskAdmin
            </span>

            <button
              onClick={logout}
              title={t('nav.signOut')}
              className="rounded-md p-1.5 text-ink-muted hover:bg-canvas hover:text-ink"
            >
              <LogOut className="size-4" />
            </button>
          </div>
        </div>
      </header>

      <div className="flex-1 min-h-0">{children}</div>
    </div>
  )
}
