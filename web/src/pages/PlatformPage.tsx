import { useState } from 'react'
import { Building2, Loader2, PauseCircle, Play, Plus, Trash2 } from 'lucide-react'
import { AppShell } from '@/components/AppShell'
import { LogoField } from '@/components/LogoField'
import { OrgLogo } from '@/components/OrgLogo'
import { ApiError } from '@/lib/api'
import { cn } from '@/lib/cn'
import { useT } from '@/lib/i18n'
import {
  useCreateOrganization,
  useDeleteOrganization,
  useOrganizations,
  useReactivateOrganization,
  useSuspendOrganization,
  type OrganizationSummary,
} from '@/lib/platform'

/** La consola de quien opera la instalación.
 *
 *  Muestra qué organizaciones existen y cómo van de uso, y nada de lo que hay adentro. Esa
 *  ausencia es la funcionalidad: es lo que permite decirle a una empresa que su trabajo no lo ve
 *  nadie más, incluido quien hospeda el servidor. */
export function PlatformPage() {
  const t = useT()
  const organizations = useOrganizations()
  const [creating, setCreating] = useState(false)

  return (
    <AppShell>
      <div className="mx-auto max-w-4xl px-5 py-8">
        <div className="flex items-start justify-between gap-4">
          <div>
            <h1 className="text-xl font-semibold tracking-tight">{t('platform.title')}</h1>
            <p className="mt-1 text-sm text-ink-muted">{t('platform.subtitle')}</p>
          </div>

          <button
            onClick={() => setCreating((v) => !v)}
            className="inline-flex shrink-0 items-center gap-1.5 rounded-lg bg-accent px-3 py-1.5
                       text-sm font-medium text-accent-ink hover:bg-accent-hover"
          >
            <Plus className="size-4" />
            {t('platform.new')}
          </button>
        </div>

        {creating && <NewOrganizationForm onDone={() => setCreating(false)} />}

        <div className="mt-8 space-y-2">
          {organizations.isLoading && (
            <p className="text-sm text-ink-muted">{t('common.loading')}</p>
          )}

          {organizations.data?.length === 0 && (
            <p className="rounded-card border border-dashed border-line px-4 py-8 text-center text-sm text-ink-muted">
              {t('platform.empty')}
            </p>
          )}

          {organizations.data?.map((organization) => (
            <OrganizationRow key={organization.id} organization={organization} />
          ))}
        </div>
      </div>
    </AppShell>
  )
}

/** 1.240.000 se lee peor que 1,2 M cuando lo que importa es el orden de magnitud. */
function formatTokens(total: number) {
  if (total >= 1_000_000) return `${(total / 1_000_000).toFixed(1)} M`
  if (total >= 1_000) return `${Math.round(total / 1_000)} k`
  return String(total)
}

function OrganizationRow({ organization }: { organization: OrganizationSummary }) {
  const t = useT()
  const suspend = useSuspendOrganization()
  const reactivate = useReactivateOrganization()
  const remove = useDeleteOrganization()

  const [asking, setAsking] = useState(false)
  const [reason, setReason] = useState('')
  const [deleting, setDeleting] = useState(false)
  const [typed, setTyped] = useState('')
  const [error, setError] = useState<string | null>(null)

  function run(promise: Promise<unknown>) {
    setError(null)
    promise
      .then(() => {
        setAsking(false)
        setDeleting(false)
      })
      .catch((err) => setError(err instanceof ApiError ? err.message : t('platform.saveFailed')))
  }

  return (
    <article
      className={cn(
        'rounded-card border border-line bg-surface px-4 py-3',
        organization.isSuspended && 'opacity-60',
      )}
    >
      <div className="flex items-center justify-between gap-4">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <OrgLogo
              organizationId={organization.id}
              hasLogo={Boolean(organization.logoPath)}
              className="size-4 shrink-0 rounded-sm object-contain"
            />
            <Building2 className="size-4 shrink-0 text-ink-subtle" />
            <span className="truncate font-medium">{organization.name}</span>
            <span className="rounded-md bg-canvas px-1.5 py-0.5 text-[11px] text-ink-muted">
              {organization.slug}
            </span>
            {organization.isSuspended && (
              <span className="text-[11px] text-stage-blocked">{t('platform.suspended')}</span>
            )}
          </div>

          <p className="mt-0.5 text-sm text-ink-muted">
            {t(organization.people === 1 ? 'platform.peopleOne' : 'platform.peopleOther', {
              count: organization.people,
            })}{' · '}
            {t(organization.projects === 1 ? 'platform.projectsOne' : 'platform.projectsOther', {
              count: organization.projects,
            })}{' · '}
            {t('platform.openItems', { count: organization.openItems })}
          </p>

          {organization.isSuspended && organization.suspendedReason && (
            <p className="mt-1 text-[12px] text-ink-subtle">
              {t('platform.reason', { reason: organization.suspendedReason })}
            </p>
          )}
        </div>

        <div className="shrink-0 text-right">
          <p className="text-[11px] text-ink-subtle">
            {organization.lastActivityAt
              ? t('platform.lastActivity', {
                  date: new Date(organization.lastActivityAt).toLocaleDateString(),
                })
              : t('platform.noActivity')}
          </p>

          {/* De quién es el costo del agente. Sin este número, el primer mes de factura alta es
              una investigación. */}
          {organization.usage.checkIns > 0 && (
            <p className="text-[11px] text-ink-subtle">
              {t('platform.usage', {
                checkIns: organization.usage.checkIns,
                tokens: formatTokens(
                  organization.usage.inputTokens +
                    organization.usage.outputTokens +
                    organization.usage.cacheReadTokens,
                ),
              })}
              {organization.usage.ownModel && ` · ${t('platform.ownModel')}`}
            </p>
          )}

          {organization.isSuspended ? (
            <div className="mt-1.5 flex items-center gap-2">
              <button
                onClick={() => run(reactivate.mutateAsync(organization.id))}
                className="inline-flex items-center gap-1.5 rounded-md border border-line px-2.5
                           py-1 text-sm hover:bg-canvas"
              >
                <Play className="size-3.5" />
                {t('platform.reactivate')}
              </button>

              {/* Borrar solo aparece con la organización ya suspendida: para llegar acá hubo que
                  cortarle el acceso antes, que es el paso que da lugar a arrepentirse. */}
              <button
                onClick={() => { setDeleting((v) => !v); setTyped('') }}
                className="inline-flex items-center gap-1.5 rounded-md border border-line px-2.5
                           py-1 text-sm text-ink-muted hover:border-stage-blocked hover:text-stage-blocked"
              >
                <Trash2 className="size-3.5" />
                {t('platform.delete')}
              </button>
            </div>
          ) : (
            <button
              onClick={() => setAsking((v) => !v)}
              className="mt-1.5 inline-flex items-center gap-1.5 rounded-md border border-line px-2.5
                         py-1 text-sm text-ink-muted hover:bg-canvas hover:text-ink"
            >
              <PauseCircle className="size-3.5" />
              {t('platform.suspend')}
            </button>
          )}
        </div>
      </div>

      {asking && (
        <div className="mt-3 rounded-lg border border-line bg-canvas px-3 py-3">
          <p className="text-sm">{t('platform.suspendWarning', { name: organization.name })}</p>

          <div className="mt-2 flex gap-2">
            <input
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder={t('platform.reasonPlaceholder')}
              className="flex-1 rounded-md border border-line bg-surface px-2.5 py-1.5 text-sm"
            />
            <button
              onClick={() => run(suspend.mutateAsync({ id: organization.id, reason }))}
              disabled={suspend.isPending}
              className="rounded-md bg-stage-blocked px-3 py-1.5 text-sm font-medium text-accent-ink"
            >
              {suspend.isPending ? (
                <Loader2 className="size-4 animate-spin" />
              ) : (
                t('platform.suspend')
              )}
            </button>
            <button
              onClick={() => setAsking(false)}
              className="rounded-md border border-line px-3 py-1.5 text-sm text-ink-muted"
            >
              {t('common.cancel')}
            </button>
          </div>
        </div>
      )}

      {deleting && (
        <div className="mt-3 rounded-lg border border-stage-blocked/40 bg-canvas px-3 py-3">
          <p className="text-sm font-medium text-stage-blocked">
            {t('platform.deleteTitle', { name: organization.name })}
          </p>
          <p className="mt-1 text-sm text-ink-muted">{t('platform.deleteWarning')}</p>

          <div className="mt-2.5 flex gap-2">
            <input
              value={typed}
              onChange={(e) => setTyped(e.target.value)}
              placeholder={organization.name}
              aria-label={t('platform.deleteConfirmLabel')}
              className="flex-1 rounded-md border border-line bg-surface px-2.5 py-1.5 text-sm"
            />
            <button
              onClick={() =>
                run(remove.mutateAsync({ id: organization.id, name: organization.name }))
              }
              // El botón no se habilita hasta que el nombre coincide exactamente. Es la
              // diferencia entre confirmar y hacer clic de más.
              disabled={typed.trim() !== organization.name || remove.isPending}
              className="rounded-md bg-stage-blocked px-3 py-1.5 text-sm font-medium text-accent-ink
                         disabled:opacity-40"
            >
              {remove.isPending ? (
                <Loader2 className="size-4 animate-spin" />
              ) : (
                t('platform.deleteConfirm')
              )}
            </button>
            <button
              onClick={() => setDeleting(false)}
              className="rounded-md border border-line px-3 py-1.5 text-sm text-ink-muted"
            >
              {t('common.cancel')}
            </button>
          </div>
        </div>
      )}

      {error && <p className="mt-2 text-sm text-stage-blocked">{error}</p>}
    </article>
  )
}

function NewOrganizationForm({ onDone }: { onDone: () => void }) {
  const t = useT()
  const create = useCreateOrganization()

  const [name, setName] = useState('')
  const [adminEmail, setAdminEmail] = useState('')
  const [adminName, setAdminName] = useState('')
  const [adminPassword, setAdminPassword] = useState('')
  const [logo, setLogo] = useState<File | null>(null)
  const [error, setError] = useState<string | null>(null)

  function submit(event: React.FormEvent) {
    event.preventDefault()
    setError(null)

    // Multipart: el logo (si hay) viaja en el mismo pedido que el alta.
    const form = new FormData()
    form.append('name', name)
    form.append('adminEmail', adminEmail)
    form.append('adminName', adminName)
    form.append('adminPassword', adminPassword)
    if (logo) form.append('logo', logo)

    create
      .mutateAsync(form)
      .then(onDone)
      .catch((err) => setError(err instanceof ApiError ? err.message : t('platform.createFailed')))
  }

  return (
    <form onSubmit={submit} className="mt-5 rounded-card border border-line bg-surface p-4">
      <p className="text-sm text-ink-muted">{t('platform.formHelp')}</p>

      <div className="mt-4 grid gap-3 sm:grid-cols-2">
        <label className="text-sm">
          <span className="text-ink-muted">{t('platform.companyName')}</span>
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
            placeholder="Acme SRL"
            className="mt-1 w-full rounded-md border border-line bg-canvas px-2.5 py-1.5"
          />
        </label>

        <label className="text-sm">
          <span className="text-ink-muted">{t('platform.adminName')}</span>
          <input
            value={adminName}
            onChange={(e) => setAdminName(e.target.value)}
            placeholder="Ana Pérez"
            className="mt-1 w-full rounded-md border border-line bg-canvas px-2.5 py-1.5"
          />
        </label>

        <label className="text-sm">
          <span className="text-ink-muted">{t('platform.adminEmail')}</span>
          <input
            type="email"
            value={adminEmail}
            onChange={(e) => setAdminEmail(e.target.value)}
            required
            placeholder="ana@acme.com"
            className="mt-1 w-full rounded-md border border-line bg-canvas px-2.5 py-1.5"
          />
        </label>

        <label className="text-sm">
          <span className="text-ink-muted">{t('platform.adminPassword')}</span>
          <input
            type="password"
            value={adminPassword}
            onChange={(e) => setAdminPassword(e.target.value)}
            required
            minLength={10}
            className="mt-1 w-full rounded-md border border-line bg-canvas px-2.5 py-1.5"
          />
        </label>

        <div className="sm:col-span-2">
          <LogoField logo={logo} onLogo={setLogo} />
        </div>
      </div>

      {error && <p className="mt-3 text-sm text-stage-blocked">{error}</p>}

      <div className="mt-4 flex gap-2">
        <button
          type="submit"
          disabled={create.isPending}
          className="inline-flex items-center gap-1.5 rounded-lg bg-accent px-3 py-1.5 text-sm
                     font-medium text-accent-ink hover:bg-accent-hover"
        >
          {create.isPending && <Loader2 className="size-4 animate-spin" />}
          {t('platform.create')}
        </button>
        <button
          type="button"
          onClick={onDone}
          className="rounded-lg border border-line px-3 py-1.5 text-sm text-ink-muted"
        >
          {t('common.cancel')}
        </button>
      </div>
    </form>
  )
}
