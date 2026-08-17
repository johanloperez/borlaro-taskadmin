import { useEffect, useState, type ReactNode } from 'react'
import { Link, useLocation } from 'react-router-dom'
import {
  Bell,
  BookOpen,
  Building2,
  History,
  LayoutGrid,
  LogOut,
  Mail,
  Menu,
  MessageSquare,
  PanelLeftClose,
  PanelLeftOpen,
  Shapes,
  ShieldCheck,
  SlidersHorizontal,
  Users,
  X,
} from 'lucide-react'
import { useTodayCheckIn } from '@/lib/agent'
import { useUnreadMessages } from '@/lib/messages'
import { useFeedUnread } from '@/lib/queries'
import { useAuth } from '@/stores/auth'
import { cn } from '@/lib/cn'
import { useT } from '@/lib/i18n'
import { LanguageSwitch } from '@/components/LanguageSwitch'
import { OrgLogo } from '@/components/OrgLogo'

/** Una entrada del menú. `badge` en 0 no se dibuja: un cero es ruido, no información. */
type NavItem = {
  to: string
  label: string
  icon: typeof LayoutGrid
  active: boolean
  badge?: number
  external?: boolean
}

const COLLAPSED_KEY = 'borlaro.nav.collapsed'

/** El armazón de la aplicación: menú lateral fijo y el contenido al lado.
 *
 *  Era una barra superior con todo en una fila, y esa disposición tiene un techo: cada pantalla
 *  nueva agrega un enlace, y cuando dejan de entrar hay que empezar a esconder cosas detrás de un
 *  «más». Un menú vertical crece hacia abajo, que es la dirección en la que sobra espacio, y
 *  además admite **grupos con título** — que es lo que hace legible una lista larga y lo que una
 *  fila horizontal no puede darte.
 *
 *  Se colapsa a solo íconos y la elección se recuerda: quien conoce el producto no necesita leer
 *  las etiquetas todos los días, y en un tablero Kanban esos 190 px son una columna más a la vista. */
export function AppShell({ children, aside }: { children: ReactNode; aside?: ReactNode }) {
  const t = useT()
  const user = useAuth((s) => s.user)
  const logout = useAuth((s) => s.logout)
  const { pathname } = useLocation()
  const today = useTodayCheckIn()
  const unread = useUnreadMessages().data?.count ?? 0
  const feedUnread = useFeedUnread().data?.count ?? 0

  const [collapsed, setCollapsed] = useState(
    () => localStorage.getItem(COLLAPSED_KEY) === '1',
  )
  const [mobileOpen, setMobileOpen] = useState(false)

  useEffect(() => {
    localStorage.setItem(COLLAPSED_KEY, collapsed ? '1' : '0')
  }, [collapsed])

  // Navegar cierra el menú en móvil. Sin esto queda el panel tapando la pantalla a la que se
  // acaba de entrar, y hay que cerrarlo a mano cada vez.
  useEffect(() => setMobileOpen(false), [pathname])

  const isOperator = user?.role === 'PlatformOperator'
  const isAdmin = user?.role === 'Admin'
  const canApprove = isAdmin || user?.role === 'Manager'

  // El operador no ve el resto de la navegación porque no tendría qué mostrarle: no pertenece a
  // ninguna empresa y todas esas pantallas le saldrían vacías. Ofrecerle enlaces que no llevan a
  // nada haría parecer roto lo que en realidad es el aislamiento funcionando.
  const groups: { title?: string; items: NavItem[] }[] = isOperator
    ? [
        {
          items: [
            {
              to: '/plataforma',
              label: t('nav.organizations'),
              icon: Building2,
              active: pathname === '/plataforma',
            },
            {
              to: '/configuracion',
              label: t('nav.installation'),
              icon: SlidersHorizontal,
              active: pathname === '/configuracion',
            },
          ],
        },
      ]
    : [
        {
          items: [
            { to: '/', label: t('nav.projects'), icon: LayoutGrid, active: pathname === '/' },
            // La actividad la ve cualquiera, acotada a sus propios proyectos: saber quién movió
            // tu tarea no es un privilegio de administración.
            {
              to: '/actividad',
              label: t('nav.activity'),
              icon: History,
              active: pathname === '/actividad',
            },
          ],
        },
        {
          // Novedades y Mensajes juntos y no separados: son las dos formas en que algo llega a
          // alguien, y partirlas hace que se mire una y se olvide la otra.
          title: t('nav.groupInbox'),
          items: [
            {
              to: '/novedades',
              label: t('nav.feed'),
              icon: Bell,
              active: pathname.startsWith('/novedades'),
              badge: feedUnread,
            },
            {
              to: '/mensajes',
              label: t('nav.messages'),
              icon: Mail,
              active: pathname.startsWith('/mensajes'),
              badge: unread,
            },
            ...(canApprove
              ? [
                  {
                    to: '/aprobaciones',
                    label: t('nav.approvals'),
                    icon: ShieldCheck,
                    active: pathname === '/aprobaciones',
                  },
                ]
              : []),
          ],
        },
        ...(isAdmin
          ? [
              {
                title: t('nav.groupAdmin'),
                items: [
                  {
                    to: '/personas',
                    label: t('nav.people'),
                    icon: Users,
                    active: pathname === '/personas',
                  },
                  {
                    to: '/plantillas',
                    label: t('nav.templates'),
                    icon: Shapes,
                    active: pathname === '/plantillas',
                  },
                  {
                    to: '/configuracion',
                    label: t('nav.settings'),
                    icon: SlidersHorizontal,
                    active: pathname === '/configuracion',
                  },
                ],
              },
            ]
          : []),
        {
          items: [
            // El manual es una página suelta, no una pantalla de la aplicación: se sirve estática
            // desde /manual y se abre en otra pestaña, para no sacar a nadie de lo que estaba
            // haciendo por ir a consultar algo.
            {
              to: '/manual',
              label: t('nav.manual'),
              icon: BookOpen,
              active: false,
              external: true,
            },
          ],
        },
      ]

  const sidebar = (
    <div className="flex h-full flex-col bg-surface">
      {/* La marca es la empresa de quien entró, no la de la plataforma. Quien usa esto todos los
          días trabaja para su empresa; el nombre del software vive chico, abajo. */}
      <div className={cn('flex h-14 shrink-0 items-center gap-2.5 border-b border-line px-3')}>
        <Link
          to={isOperator ? '/plataforma' : '/'}
          title={user?.organizationName ?? undefined}
          className="flex min-w-0 flex-1 items-center gap-2.5"
        >
          {!isOperator && user?.organizationName ? (
            <>
              <OrgLogo
                organizationId={user.organizationId}
                hasLogo={Boolean(user.logoPath)}
                name={user.organizationName}
                className="size-9 shrink-0 rounded-md object-contain"
              />
              {!collapsed && (
                <span className="truncate text-base font-semibold tracking-tight">
                  {user.organizationName}
                </span>
              )}
            </>
          ) : (
            !collapsed && (
              <span className="truncate text-base font-semibold tracking-tight">Borlaro TMS</span>
            )
          )}
        </Link>

        <button
          onClick={() => setMobileOpen(false)}
          title={t('nav.closeMenu')}
          className="rounded-md p-1.5 text-ink-muted hover:bg-canvas hover:text-ink lg:hidden"
        >
          <X className="size-4" />
        </button>
      </div>

      <nav className="min-h-0 flex-1 overflow-y-auto px-2 py-3">
        {groups.map((group, i) => (
          <div key={i} className={cn(i > 0 && 'mt-4')}>
            {/* El título del grupo desaparece al colapsar: sobre íconos solos no explica nada y
                deja una franja de texto cortado. La separación entre grupos alcanza. */}
            {group.title && !collapsed && (
              <p className="px-2.5 pb-1 text-[11px] font-medium uppercase tracking-wide text-ink-subtle">
                {group.title}
              </p>
            )}
            <ul className="space-y-0.5">
              {group.items.map((item) => (
                <li key={item.to}>
                  <NavLink item={item} collapsed={collapsed} />
                </li>
              ))}
            </ul>
          </div>
        ))}
      </nav>

      <div className="shrink-0 space-y-2 border-t border-line px-2 py-3">
        {/* El check-in pendiente se ve desde cualquier pantalla: si hay que ir a buscarlo, no se
            hace. Va arriba del pie porque es lo único de acá que pide una acción hoy. */}
        {today.data && (
          <Link
            to={`/checkin/${today.data.id}`}
            title={t('nav.todayCheckIn')}
            className={cn(
              'flex items-center gap-2.5 rounded-md bg-accent-soft px-2.5 py-2 text-sm text-ink',
              collapsed && 'justify-center px-0',
            )}
          >
            <MessageSquare className="size-4 shrink-0" />
            {!collapsed && <span className="truncate">{t('nav.todayCheckIn')}</span>}
          </Link>
        )}

        <div
          className={cn(
            'flex items-center gap-2',
            collapsed ? 'flex-col' : 'justify-between',
          )}
        >
          {!collapsed && (
            <span className="min-w-0 truncate text-sm text-ink-muted" title={user?.name}>
              {user?.name}
            </span>
          )}

          <div className={cn('flex items-center gap-1', collapsed && 'flex-col')}>
            {!collapsed && <LanguageSwitch compact />}
            <button
              onClick={logout}
              title={t('nav.signOut')}
              className="rounded-md p-1.5 text-ink-muted hover:bg-canvas hover:text-ink"
            >
              <LogOut className="size-4" />
            </button>
          </div>
        </div>

        {isOperator && !collapsed && (
          <span className="block rounded-md bg-canvas px-1.5 py-0.5 text-center text-[11px] text-ink-subtle">
            {t('nav.operatorBadge')}
          </span>
        )}

        {/* La marca de la plataforma, chica y sin enlace: es un dato de quién provee el software,
            no un lugar al que ir. */}
        {!collapsed && !isOperator && (
          <span className="block px-2.5 text-[11px] tracking-wide text-ink-subtle">Borlaro TMS</span>
        )}

        <button
          onClick={() => setCollapsed((v) => !v)}
          title={collapsed ? t('nav.expandMenu') : t('nav.collapseMenu')}
          className={cn(
            'hidden w-full items-center gap-2.5 rounded-md px-2.5 py-1.5 text-sm text-ink-muted',
            'hover:bg-canvas hover:text-ink lg:flex',
            collapsed && 'justify-center px-0',
          )}
        >
          {collapsed ? (
            <PanelLeftOpen className="size-4 shrink-0" />
          ) : (
            <>
              <PanelLeftClose className="size-4 shrink-0" />
              <span>{t('nav.collapseMenu')}</span>
            </>
          )}
        </button>
      </div>
    </div>
  )

  return (
    <div className="flex min-h-screen">
      {/* Fijo y con scroll propio: la navegación no se va de la pantalla al bajar en una lista
          larga, que es la mitad de la razón para tenerla al costado. */}
      <aside
        className={cn(
          'hidden shrink-0 border-r border-line lg:block',
          'sticky top-0 h-screen',
          collapsed ? 'w-16' : 'w-60',
        )}
      >
        {sidebar}
      </aside>

      {/* En móvil el mismo menú es un panel que se desliza. Un menú lateral fijo en 375 px se
          comería media pantalla. */}
      {mobileOpen && (
        <>
          <div
            onClick={() => setMobileOpen(false)}
            className="fixed inset-0 z-30 bg-ink/30 lg:hidden"
          />
          <aside className="fixed inset-y-0 left-0 z-40 w-64 border-r border-line lg:hidden">
            {sidebar}
          </aside>
        </>
      )}

      <div className="flex min-w-0 flex-1 flex-col">
        {/* La barra de arriba queda solo para lo que es de la pantalla: las acciones del tablero,
            el botón del menú en móvil. Todo lo que era navegación se fue al costado. */}
        <header className="flex h-14 shrink-0 items-center gap-3 border-b border-line bg-surface px-4 lg:px-5">
          <button
            onClick={() => setMobileOpen(true)}
            title={t('nav.openMenu')}
            className="rounded-md p-1.5 text-ink-muted hover:bg-canvas hover:text-ink lg:hidden"
          >
            <Menu className="size-5" />
          </button>

          <div className="ml-auto flex items-center gap-3 text-sm">{aside}</div>
        </header>

        <div className="min-h-0 flex-1">{children}</div>
      </div>
    </div>
  )
}

function NavLink({ item, collapsed }: { item: NavItem; collapsed: boolean }) {
  const className = cn(
    'flex items-center gap-2.5 rounded-md px-2.5 py-2 text-sm',
    'hover:bg-canvas',
    item.active ? 'bg-canvas font-medium text-ink' : 'text-ink-muted',
    collapsed && 'justify-center px-0',
  )

  const inside = (
    <>
      <item.icon className="size-4 shrink-0" />
      {!collapsed && <span className="min-w-0 flex-1 truncate">{item.label}</span>}
      {item.badge !== undefined && item.badge > 0 && (
        <span
          className={cn(
            'rounded-full bg-accent px-1.5 text-[11px] text-accent-ink',
            // Colapsado el número no entra al lado del ícono, así que se monta encima.
            collapsed && 'absolute right-1 top-1 px-1',
          )}
        >
          {item.badge}
        </span>
      )}
    </>
  )

  // El manual se abre en otra pestaña, para no sacar a nadie de lo que estaba haciendo.
  if (item.external) {
    return (
      <a
        href={item.to}
        target="_blank"
        rel="noopener"
        title={collapsed ? item.label : undefined}
        className={className}
      >
        {inside}
      </a>
    )
  }

  return (
    <Link
      to={item.to}
      title={collapsed ? item.label : undefined}
      className={cn(className, collapsed && 'relative')}
    >
      {inside}
    </Link>
  )
}
