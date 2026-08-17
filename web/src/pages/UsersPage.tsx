import { useState, type FormEvent } from 'react'
import { ChevronRight, KeyRound, Link2Off, Loader2, Plus, Trash2, UserMinus, UserPlus } from 'lucide-react'
import { AppShell } from '@/components/AppShell'
import { ApiError } from '@/lib/api'
import { cn } from '@/lib/cn'
import { useT, type Translate } from '@/lib/i18n'
import {
  WEEK_DAYS,
  hasDay,
  toggleDay,
  useAdminUsers,
  useCreateUser,
  useDeactivateUser,
  useDeleteUser,
  useEditUser,
  useOidcInfo,
  useResetPassword,
  useUnlinkExternal,
  type AdminUser,
} from '@/lib/admin'
import { browserTimeZone, knownTimeZone, timeZoneGroups } from '@/lib/timezones'
import type { UserRole } from '@/stores/auth'

/** Mismo mínimo que valida el backend. Duplicarlo acá es a propósito: la UI tiene que poder
 *  decir qué falta antes de mandar, no esperar un 400 para enterarse. */
const MIN_PASSWORD = 10

/** Los roles guardan claves, no textos: el nombre y la explicación de cada uno cambian con el
 *  idioma, pero el valor que viaja al servidor no. */
const ROLES = [
  { value: 'Admin', label: 'role.admin', hint: 'role.adminHint' },
  { value: 'Manager', label: 'role.manager', hint: 'role.managerHint' },
  { value: 'Collaborator', label: 'role.collaborator', hint: 'role.collaboratorHint' },
  { value: 'ClientReviewer', label: 'role.clientReviewer', hint: 'role.clientReviewerHint' },
] as const satisfies readonly { value: UserRole; label: Parameters<Translate>[0]; hint: Parameters<Translate>[0] }[]

const roleLabel = (t: Translate, role: UserRole) => {
  const found = ROLES.find((r) => r.value === role)
  return found ? t(found.label) : role
}

const roleHint = (t: Translate, role: UserRole) => {
  const found = ROLES.find((r) => r.value === role)
  return found ? t(found.hint) : undefined
}

/** Alta y gestión de personas. Es la pantalla que faltaba para que la instancia se opere sin
 *  tocar la base: sin ella, sumar a alguien al equipo era un INSERT a mano. */
export function UsersPage() {
  const t = useT()
  const users = useAdminUsers()
  const [creating, setCreating] = useState(false)

  return (
    <AppShell>
      <div className="mx-auto max-w-4xl px-6 py-10">
        <div className="flex items-end justify-between">
          <div>
            <h1 className="text-xl font-semibold tracking-tight">{t('people.title')}</h1>
            <p className="mt-1 text-sm text-ink-muted">{t('people.subtitle')}</p>
          </div>
          <button
            onClick={() => setCreating((v) => !v)}
            className="inline-flex items-center gap-1.5 rounded-lg bg-accent px-3 py-1.5 text-sm
                       font-medium text-accent-ink hover:bg-accent-hover"
          >
            <Plus className="size-4" />
            {t('people.new')}
          </button>
        </div>

        {creating && <NewUserForm onDone={() => setCreating(false)} />}

        <div className="mt-8 space-y-2">
          {users.isLoading && <p className="text-sm text-ink-muted">{t('common.loading')}</p>}
          {users.data?.map((user) => (
            <UserRow key={user.id} user={user} />
          ))}
        </div>
      </div>
    </AppShell>
  )
}

function UserRow({ user }: { user: AdminUser }) {
  const t = useT()
  const edit = useEditUser()
  const deactivate = useDeactivateUser()
  const remove = useDeleteUser()
  const resetPassword = useResetPassword()
  const unlink = useUnlinkExternal()

  const [expanded, setExpanded] = useState(false)
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState<string | null>(null)

  function run(promise: Promise<unknown>, okMessage?: string) {
    setError(null)
    setDone(null)
    promise
      .then(() => okMessage && setDone(okMessage))
      .catch((err) => setError(err instanceof ApiError ? err.message : t('people.saveFailed')))
  }

  return (
    <article
      className={cn(
        'rounded-card border border-line bg-surface px-4 py-3',
        !user.isActive && 'opacity-60',
      )}
    >
      <div className="flex items-center justify-between gap-4">
        <button
          onClick={() => setExpanded((v) => !v)}
          title={expanded ? t('people.closeCard') : t('people.openCard')}
          className="flex min-w-0 flex-1 items-start gap-2 text-left"
        >
          {/* Sin esta flecha, nada dice que la fila se abre — y todas las acciones sobre la
              persona viven adentro. */}
          <ChevronRight
            className={cn('mt-0.5 size-4 shrink-0 text-ink-subtle transition-transform', expanded && 'rotate-90')}
          />

          <span className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <span className="font-medium truncate">{user.name}</span>
            <span className="rounded-md bg-canvas px-1.5 py-0.5 text-[11px] text-ink-muted">
              {roleLabel(t, user.role)}
            </span>
            {!user.isActive && (
              <span className="text-[11px] text-stage-blocked">{t('people.deactivated')}</span>
            )}
            {/* Por qué está acá: es lo que responde «¿por qué esta persona no puede entrar?»
                sin tener que mirar la base. */}
            {user.externalLogin && (
              <span className="rounded-md bg-canvas px-1.5 py-0.5 text-[11px] text-ink-subtle">
                {user.hasPassword ? t('people.linked') : t('people.externalOnly')}
              </span>
            )}
            {!user.hasPassword && !user.externalLogin && (
              <span className="text-[11px] text-stage-progress">{t('people.noCredentials')}</span>
            )}
          </div>
          <p className="mt-0.5 text-sm text-ink-muted truncate">{user.email}</p>
          </span>
        </button>

        <div className="shrink-0 text-right text-sm text-ink-muted">
          <p>{t('people.openItems', { count: user.openItems })}</p>
          <p className="text-[11px] text-ink-subtle">
            {user.checkInsEnabled
              ? t('people.checkInAt', { time: user.checkInTime.slice(0, 5) })
              : t('people.noCheckIn')}
          </p>
        </div>
      </div>

      {expanded && (
        <div className="mt-4 space-y-4 border-t border-line pt-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label={t('people.role')}>
              <select
                value={user.role}
                onChange={(e) => run(edit.mutateAsync({ id: user.id, role: e.target.value as UserRole }))}
                className="w-full rounded-lg border border-line bg-canvas px-3 py-2 text-sm"
              >
                {ROLES.map((r) => (
                  <option key={r.value} value={r.value}>
                    {t(r.label)}
                  </option>
                ))}
              </select>
            </Field>

            <Field label={t('people.timeZone')}>
              <TimeZoneSelect
                value={user.timeZoneId}
                onChange={(id) => id !== user.timeZoneId && run(edit.mutateAsync({ id: user.id, timeZoneId: id }))}
              />
            </Field>

            <Field label={t('people.checkInTime')}>
              <input
                type="time"
                defaultValue={user.checkInTime.slice(0, 5)}
                onBlur={(e) =>
                  run(edit.mutateAsync({ id: user.id, checkInTime: `${e.target.value}:00` }))
                }
                className="w-full rounded-lg border border-line bg-canvas px-3 py-2 text-sm"
              />
            </Field>

            <Field label={t('people.workDays')}>
              <div className="flex gap-1">
                {WEEK_DAYS.map((day) => (
                  <button
                    key={day.bit}
                    title={t(day.label)}
                    onClick={() =>
                      run(edit.mutateAsync({ id: user.id, workDaysMask: toggleDay(user.workDaysMask, day.bit) }))
                    }
                    className={cn(
                      'size-8 rounded-md border text-xs',
                      hasDay(user.workDaysMask, day.bit)
                        ? 'border-accent bg-accent-soft/50 text-ink'
                        : 'border-line text-ink-subtle',
                    )}
                  >
                    {t(day.short)}
                  </button>
                ))}
              </div>
            </Field>
          </div>

          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={user.checkInsEnabled}
              onChange={(e) => run(edit.mutateAsync({ id: user.id, checkInsEnabled: e.target.checked }))}
            />
            {t('people.receivesCheckIn')}
          </label>

          {/* Los tres permisos por persona. Se comprueban además del permiso sobre el proyecto:
              encenderlos no mete a nadie en un tablero ajeno, solo deciden qué puede hacer ahí
              donde ya entra. Nacen encendidos porque son restricciones, no concesiones — y por eso
              se apagan acá, de a uno, cuando alguien lo decide.

              Están en esta pantalla y no en una aparte porque un permiso que nadie encuentra es
              igual a uno que no existe. */}
          <div className="space-y-1">
            <p className="text-xs font-medium text-ink-muted">{t('users.permissions')}</p>

            <div className="flex flex-wrap gap-x-4 gap-y-1">
              {(
                [
                  ['canCreateTasks', 'users.canCreateTasks'],
                  ['canAssignTasks', 'users.canAssignTasks'],
                  ['canSetDueDate', 'users.canSetDueDate'],
                ] as const
              ).map(([key, label]) => (
                <label key={key} className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={user[key]}
                    onChange={(e) => run(edit.mutateAsync({ id: user.id, [key]: e.target.checked }))}
                  />
                  {t(label)}
                </label>
              ))}
            </div>
          </div>

          {/* Los tres controles en una sola fila y con la misma altura: el texto de ayuda va
              debajo, no adentro, porque si crece empuja a uno solo y desalinea la fila. */}
          <div className="space-y-1">
            <div className="flex flex-wrap items-center gap-2">
              <input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder={t('people.newPassword')}
                className="h-9 rounded-lg border border-line bg-canvas px-3 text-sm"
              />
              <button
                onClick={() => {
                  run(resetPassword.mutateAsync({ id: user.id, password }), t('people.passwordChanged'))
                  setPassword('')
                }}
                disabled={password.length < MIN_PASSWORD}
                title={
                  password.length < MIN_PASSWORD
                    ? t('people.passwordTooShort', { min: MIN_PASSWORD })
                    : t('people.changePasswordHint')
                }
                className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-line px-3
                           text-sm text-ink-muted disabled:cursor-not-allowed disabled:opacity-40"
              >
                <KeyRound className="size-4" />
                {t('people.changePassword')}
              </button>

              {user.isActive ? (
                <button
                  onClick={() => run(deactivate.mutateAsync(user.id))}
                  className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-line px-3
                             text-sm text-stage-blocked"
                >
                  <UserMinus className="size-4" />
                  {t('people.deactivate')}
                </button>
              ) : (
                <button
                  onClick={() => run(edit.mutateAsync({ id: user.id, isActive: true }))}
                  className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-line px-3 text-sm"
                >
                  <UserPlus className="size-4" />
                  {t('people.reactivate')}
                </button>
              )}

              {/* Borrado definitivo: el backend solo lo permite si la persona no dejó rastro, y
                  si lo dejó responde diciendo exactamente qué. Por eso el botón está siempre y
                  el «no se puede» llega como explicación, no como botón ausente. */}
              <button
                onClick={() => run(remove.mutateAsync(user.id), t('people.deleted'))}
                title={t('people.deleteHint')}
                className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-line px-3
                           text-sm text-ink-muted hover:text-stage-blocked"
              >
                <Trash2 className="size-4" />
                {t('people.delete')}
              </button>
            </div>

            {user.externalLogin && (
              <div className="flex flex-wrap items-center gap-2 pt-1">
                <button
                  onClick={() =>
                    run(unlink.mutateAsync(user.id), t('people.unlinked'))
                  }
                  className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-line px-3
                             text-sm text-ink-muted"
                >
                  <Link2Off className="size-4" />
                  {t('people.unlink')}
                </button>
                <span className="text-[11px] text-ink-subtle">
                  {t('people.unlinkHint', { account: user.externalLogin })}
                </span>
              </div>
            )}

            {/* Un botón apagado sin explicación parece roto. Se dice qué falta y cuánto. */}
            <p className="text-[11px] text-ink-subtle">
              {password.length === 0
                ? t('people.typePassword', { min: MIN_PASSWORD })
                : password.length < MIN_PASSWORD
                  ? t('people.missingChars', { n: MIN_PASSWORD - password.length })
                  : t('people.readyToChange')}
            </p>
          </div>

          {/* Desactivar no borra: el nombre de la persona cuelga de tareas, comentarios y
              transcripts, y un borrado dejaría el historial hablando de un fantasma. */}
          <p className="text-[11px] text-ink-subtle">{t('people.deactivateNote')}</p>

          {error && <p className="text-sm text-stage-blocked">{error}</p>}
          {done && <p className="text-sm text-stage-done">{done}</p>}
        </div>
      )}
    </article>
  )
}

function NewUserForm({ onDone }: { onDone: () => void }) {
  const t = useT()
  const create = useCreateUser()
  const oidc = useOidcInfo()
  const [email, setEmail] = useState('')
  const [name, setName] = useState('')
  const [password, setPassword] = useState('')
  const [external, setExternal] = useState(false)
  const [role, setRole] = useState<UserRole>('Collaborator')
  const [timeZoneId, setTimeZoneId] = useState(browserTimeZone)
  const [checkInTime, setCheckInTime] = useState('09:00')
  const [workDaysMask, setWorkDaysMask] = useState(0b0111110)
  const [error, setError] = useState<string | null>(null)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    try {
      await create.mutateAsync({
        email,
        name,
        // Sin contraseña la cuenta entra solo por el proveedor: se vincula sola la primera vez
        // que la persona entra con ese mismo email.
        password: external ? undefined : password,
        role,
        timeZoneId,
        checkInTime: `${checkInTime}:00`,
        workDaysMask,
      })
      onDone()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('people.createFailed'))
    }
  }

  return (
    <form onSubmit={submit} className="mt-6 rounded-card border border-line bg-surface p-5 space-y-4">
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label={t('people.name')}>
          <input
            required
            value={name}
            onChange={(e) => setName(e.target.value)}
            className="w-full rounded-lg border border-line bg-canvas px-3 py-2 text-sm"
          />
        </Field>
        <Field label={t('common.email')}>
          <input
            required
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="w-full rounded-lg border border-line bg-canvas px-3 py-2 text-sm"
          />
        </Field>
        <Field
          label={t('people.initialPassword')}
          hint={
            external
              ? t('people.externalNoPassword', {
                  provider: oidc.data?.label ?? t('people.externalProvider'),
                })
              : password.length === 0 || password.length >= MIN_PASSWORD
                ? t('people.passwordHint', { min: MIN_PASSWORD })
                : t('people.missingChars', { n: MIN_PASSWORD - password.length })
          }
        >
          <input
            required={!external}
            disabled={external}
            minLength={MIN_PASSWORD}
            type="password"
            value={external ? '' : password}
            onChange={(e) => setPassword(e.target.value)}
            className="w-full rounded-lg border border-line bg-canvas px-3 py-2 text-sm
                       disabled:opacity-40"
          />
        </Field>
        <Field label={t('people.role')} hint={roleHint(t, role)}>
          <select
            value={role}
            onChange={(e) => setRole(e.target.value as UserRole)}
            className="w-full rounded-lg border border-line bg-canvas px-3 py-2 text-sm"
          >
            {ROLES.map((r) => (
              <option key={r.value} value={r.value}>
                {t(r.label)}
              </option>
            ))}
          </select>
        </Field>
        <Field label={t('people.timeZone')} hint={t('people.timeZoneHint')}>
          <TimeZoneSelect value={timeZoneId} onChange={setTimeZoneId} />
        </Field>
        <Field label={t('people.checkInTime')}>
          <input
            type="time"
            value={checkInTime}
            onChange={(e) => setCheckInTime(e.target.value)}
            className="w-full rounded-lg border border-line bg-canvas px-3 py-2 text-sm"
          />
        </Field>
      </div>

      {/* Solo aparece si hay proveedor configurado y encendido. Inventarle una contraseña a
          alguien que va a entrar con Google es una contraseña más dando vueltas por un chat, y
          que nadie usa nunca. */}
      {oidc.data?.enabled && (
        <label className="flex items-start gap-2 text-sm">
          <input
            type="checkbox"
            checked={external}
            onChange={(e) => setExternal(e.target.checked)}
            className="mt-0.5"
          />
          <span>
            {t('people.externalCheckbox', {
              provider: oidc.data.label.replace(/^entrar con |^sign in with /i, ''),
            })}
            <span className="block text-[11px] text-ink-subtle">
              {t('people.externalCheckboxHint')}
            </span>
          </span>
        </label>
      )}

      <Field label={t('people.workDays')}>
        <div className="flex gap-1">
          {WEEK_DAYS.map((day) => (
            <button
              type="button"
              key={day.bit}
              title={t(day.label)}
              onClick={() => setWorkDaysMask((m) => toggleDay(m, day.bit))}
              className={cn(
                'size-8 rounded-md border text-xs',
                hasDay(workDaysMask, day.bit)
                  ? 'border-accent bg-accent-soft/50 text-ink'
                  : 'border-line text-ink-subtle',
              )}
            >
              {t(day.short)}
            </button>
          ))}
        </div>
      </Field>

      {error && <p className="text-sm text-stage-blocked">{error}</p>}

      <div className="flex items-center gap-2">
        <button
          type="submit"
          disabled={create.isPending}
          className="inline-flex items-center gap-2 rounded-lg bg-accent px-3 py-1.5 text-sm
                     font-medium text-accent-ink hover:bg-accent-hover disabled:opacity-50"
        >
          {create.isPending && <Loader2 className="size-4 animate-spin" />}
          {t('people.create')}
        </button>
        <button type="button" onClick={onDone} className="rounded-lg px-3 py-1.5 text-sm text-ink-muted">
          {t('common.cancel')}
        </button>
      </div>
    </form>
  )
}

/** Lista de zonas agrupada por región. Si el usuario tiene guardada una zona que este navegador
 *  no conoce, se ofrece igual como primera opción: sin eso el select mostraría otra cosa de la que
 *  está guardada, y el primer blur la sobreescribiría sin que nadie la haya tocado. */
function TimeZoneSelect({ value, onChange }: { value: string; onChange: (id: string) => void }) {
  const groups = timeZoneGroups()
  return (
    <select
      value={value}
      onChange={(e) => onChange(e.target.value)}
      className="w-full rounded-lg border border-line bg-canvas px-3 py-2 text-sm"
    >
      {!knownTimeZone(value) && <option value={value}>{value}</option>}
      {groups.map((group) => (
        <optgroup key={group.region} label={group.region}>
          {group.zones.map((zone) => (
            <option key={zone.id} value={zone.id}>
              {zone.label}
            </option>
          ))}
        </optgroup>
      ))}
    </select>
  )
}

function Field({ label, hint, children }: { label: string; hint?: string; children: React.ReactNode }) {
  return (
    <div className="space-y-1.5">
      <p className="text-sm font-medium">{label}</p>
      {children}
      {hint && <p className="text-[11px] text-ink-subtle">{hint}</p>}
    </div>
  )
}
