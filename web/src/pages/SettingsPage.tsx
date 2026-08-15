import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AlertTriangle, Check, Loader2, RefreshCw } from 'lucide-react'
import { AppShell } from '@/components/AppShell'
import { api, ApiError } from '@/lib/api'
import { cn } from '@/lib/cn'
import { useAuth } from '@/stores/auth'
import { useT, type Translate } from '@/lib/i18n'

type SettingKind = 'Text' | 'Number' | 'Boolean' | 'Secret' | 'Select'

/** Los grupos llegan del servidor como identificadores estables, no como texto: el nombre que
 *  se lee sale del diccionario. Sin esto, comparar `group === 'Modelo de IA'` dejaría de
 *  funcionar en cuanto la pantalla se mirara en otro idioma. */
const GROUP_KEY: Record<string, Parameters<Translate>[0]> = {
  'Check-ins': 'settings.groupCheckIns',
  'Escalera de entrega': 'settings.groupLadder',
  Email: 'settings.groupEmail',
  'Modelo de IA': 'settings.groupModel',
  Almacenamiento: 'settings.groupStorage',
  Enlaces: 'settings.groupLinks',
  'Sesión': 'settings.groupSession',
  'Inicio de sesión': 'settings.groupLogin',
}

/** El texto de un ajuste, traducido por su clave. Si falta la traducción se muestra lo que mandó
 *  el servidor: un ajuste nuevo sin traducir se lee en castellano, que es mucho mejor que
 *  mostrar «settings.Foo:Bar.label». */
function settingText(t: Translate, key: string, part: 'label' | 'help', fallback: string) {
  const dictionaryKey = `settings.${key}.${part}` as Parameters<Translate>[0]
  const translated = t(dictionaryKey)
  return translated === dictionaryKey ? fallback : translated
}

interface Setting {
  key: string
  group: string
  label: string
  help: string
  kind: SettingKind
  options: string[] | null
  value: string | null
  isSet: boolean
  isOverridden: boolean
  updatedAt: string | null
}

interface ModelStatus {
  configured: string
  effective: string
  model: string
  usingFallback: boolean
  explanation: string
}

/** La misma pantalla sirve a dos autoridades: el admin de una organización edita lo suyo, y el
 *  operador de la instalación lo que es de todos. Cambia la ruta, no el formulario — duplicar
 *  esta pantalla habría garantizado que una de las dos quedara vieja. */
function useSettingsBase() {
  const role = useAuth((s) => s.user?.role)
  return role === 'PlatformOperator' ? '/api/platform/settings' : '/api/settings'
}

function useSettings(base: string) {
  return useQuery({ queryKey: ['settings', base], queryFn: () => api<Setting[]>(base) })
}

interface LocalModels {
  models: string[]
  baseUrl: string
  error: string | null
}

/** Qué modelo corre de verdad. La configuración dice la intención; esto dice el hecho, y no
 *  coinciden cuando falta la clave y el agente cae al modelo de prueba. */
function useModelStatus(enabled: boolean) {
  return useQuery({
    queryKey: ['settings', 'model'],
    queryFn: () => api<ModelStatus>('/api/settings/model'),
    // El operador de la instalación no tiene modelo propio que mirar: el agente es de cada
    // organización. Pedirlo le devolvería un 403 que no significa nada.
    enabled,
  })
}

/** Los modelos instalados en el servidor local. Solo se pide cuando el admin lo pide: si se
 *  consultara solo, cada visita a Configuración esperaría el timeout de un servidor que quizá
 *  no está. */
function useLocalModels() {
  return useMutation({
    mutationFn: () => api<LocalModels>('/api/settings/model/available'),
  })
}

interface OidcProbe {
  enabled: boolean
  complete: boolean
  /** La URL que hay que dar de alta en la consola del proveedor. */
  redirectUri: string
  issuer: string | null
  error: string | null
}

/** Igual que con el modelo: la configuración dice la intención, esto dice el hecho. Que el
 *  descubrimiento responda es la diferencia entre «guardado» y «funciona», y sin este cartel la
 *  diferencia aparece recién cuando alguien intenta entrar. */
function useOidcProbe(base: string) {
  return useQuery({ queryKey: ['settings', 'oidc', base], queryFn: () => api<OidcProbe>(`${base}/oidc`) })
}

function useSaveSettings(base: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (values: Record<string, string | null>) =>
      api<Setting[]>(base, { method: 'PUT', body: { values } }),
    onSuccess: (fresh) => qc.setQueryData(['settings', base], fresh),
  })
}

/** Configuración de la instancia. La regla que la justifica: si algo es configurable, se
 *  configura acá. Un ajuste que solo existe en un archivo del servidor obliga a tener acceso al
 *  servidor para cambiar el horario de un check-in. */
export function SettingsPage() {
  const base = useSettingsBase()
  const isPlatform = base === '/api/platform/settings'

  const t = useT()
  const settings = useSettings(base)
  const status = useModelStatus(!isPlatform)
  const local = useLocalModels()
  const oidc = useOidcProbe(base)
  const save = useSaveSettings(base)

  const [draft, setDraft] = useState<Record<string, string | null>>({})
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)

  const groups = [...new Set(settings.data?.map((s) => s.group) ?? [])]
  const dirty = Object.keys(draft).length > 0

  async function submit() {
    setError(null)
    setSaved(false)
    try {
      await save.mutateAsync(draft)
      setDraft({})
      setSaved(true)
      // Cambiar proveedor, modelo o clave cambia qué corre: los carteles de estado tienen que
      // reflejarlo sin recargar la página.
      void status.refetch()
      void oidc.refetch()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('settings.saveFailed'))
    }
  }

  return (
    <AppShell>
      <div className="mx-auto max-w-3xl px-6 py-10 pb-28">
        <h1 className="text-xl font-semibold tracking-tight">
          {isPlatform ? t('settings.platformTitle') : t('settings.title')}
        </h1>
        <p className="mt-1 text-sm text-ink-muted">
          {isPlatform ? t('settings.platformSubtitle') : t('settings.subtitle')}
        </p>

        {settings.isLoading && <p className="mt-8 text-sm text-ink-muted">{t('common.loading')}</p>}

        {groups.map((group) => (
          <section key={group} className="mt-8">
            <h2 className="text-sm font-semibold text-ink">
              {GROUP_KEY[group] ? t(GROUP_KEY[group]) : group}
            </h2>

            {group === 'Modelo de IA' && status.data && (
              <div
                className={cn(
                  'mt-2 flex items-start gap-2 rounded-card border p-3 text-xs',
                  status.data.usingFallback
                    ? 'border-stage-progress/40 bg-stage-progress/5'
                    : 'border-line bg-surface',
                )}
              >
                {status.data.usingFallback ? (
                  <AlertTriangle className="mt-0.5 size-4 shrink-0 text-stage-progress" />
                ) : (
                  <Check className="mt-0.5 size-4 shrink-0 text-stage-done" />
                )}
                <p className="text-ink-muted">
                  <span className="text-ink">
                    {t('settings.runningNow')}{' '}
                    <strong>
                      {status.data.effective === 'scripted'
                        ? t('settings.testModel')
                        : status.data.model || status.data.effective}
                    </strong>
                  </span>{' '}
                  {status.data.explanation}
                </p>
              </div>
            )}

            {group === 'Modelo de IA' && (
              <div className="mt-2 rounded-card border border-line bg-surface p-3">
                <div className="flex flex-wrap items-center gap-2">
                  <button
                    onClick={() => local.mutate()}
                    disabled={local.isPending}
                    className="inline-flex items-center gap-1.5 rounded-lg border border-line px-2.5 py-1.5
                               text-xs text-ink-muted hover:bg-canvas hover:text-ink disabled:opacity-50"
                  >
                    {local.isPending ? (
                      <Loader2 className="size-3.5 animate-spin" />
                    ) : (
                      <RefreshCw className="size-3.5" />
                    )}
                    {t('settings.detectModels')}
                  </button>
                  <span className="text-[11px] text-ink-subtle">{t('settings.detectHelp')}</span>
                </div>

                {local.data?.error && (
                  <p className="mt-2 text-xs text-stage-blocked">{local.data.error}</p>
                )}

                {local.data && !local.data.error && (
                  <p className="mt-2 text-xs text-ink-muted">
                    {t('settings.detected', {
                      count: local.data.models.length,
                      url: local.data.baseUrl,
                    })}
                  </p>
                )}
              </div>
            )}

            {group === 'Inicio de sesión' && oidc.data && (
              <div className="mt-2 space-y-2 rounded-card border border-line bg-surface p-3 text-xs">
                <div className="flex items-start gap-2">
                  {oidc.data.error ? (
                    <AlertTriangle className="mt-0.5 size-4 shrink-0 text-stage-progress" />
                  ) : (
                    <Check className="mt-0.5 size-4 shrink-0 text-stage-done" />
                  )}
                  <p className="text-ink-muted">
                    {oidc.data.error ? (
                      oidc.data.error
                    ) : (
                      <>
                        <span className="text-ink">{t('settings.providerAnswers')}</span>{' '}
                        {t('settings.providerAs')}{' '}
                        <code className="text-ink">{oidc.data.issuer}</code>.
                        {!oidc.data.enabled && t('settings.providerOff')}
                      </>
                    )}
                  </p>
                </div>

                {/* Pegar mal esta URL es el error más común de la integración, y el proveedor lo
                    reporta como redirect_uri_mismatch sin decir cuál esperaba. */}
                <p className="text-ink-muted">
                  {t('settings.redirectUri')}{' '}
                  <code className="select-all break-all text-ink">{oidc.data.redirectUri}</code>
                </p>

                <p className="text-ink-subtle">{t('settings.oidcNoSignUp')}</p>
              </div>
            )}

            <div className="mt-3 divide-y divide-line rounded-card border border-line bg-surface">
              {settings.data
                ?.filter((s) => s.group === group)
                .map((setting) => (
                  <Row
                    key={setting.key}
                    t={t}
                    setting={setting}
                    draft={draft[setting.key]}
                    detected={setting.key === 'AgentModel:Model' ? local.data?.models : undefined}
                    onChange={(value) => setDraft((d) => ({ ...d, [setting.key]: value }))}
                  />
                ))}
            </div>
          </section>
        ))}

        {/* Lo que no se puede mover desde acá, dicho de frente en vez de que alguien lo busque
            veinte minutos. */}
        <div className="mt-8 space-y-2 rounded-card border border-dashed border-line-strong p-4 text-xs text-ink-muted">
          <p>
            <strong className="text-ink">{t('settings.cannotChangeTitle')}</strong>
            {t('settings.cannotChangeAnd')}
          </p>
          <p>{t('settings.cannotChangeBoot')}</p>
          <p>{t('settings.cannotChangeStartup')}</p>
        </div>
      </div>

      {(dirty || saved || error) && (
        <div className="fixed inset-x-0 bottom-0 border-t border-line bg-surface">
          <div className="mx-auto flex max-w-3xl items-center justify-between gap-4 px-6 py-3">
            <p className="text-sm text-ink-muted">
              {error ? (
                <span className="text-stage-blocked">{error}</span>
              ) : saved && !dirty ? (
                <span className="inline-flex items-center gap-1.5 text-stage-done">
                  <Check className="size-4" /> {t('settings.savedApplied')}
                </span>
              ) : (
                t('settings.unsaved', { count: Object.keys(draft).length })
              )}
            </p>

            {dirty && (
              <div className="flex gap-2">
                <button
                  onClick={() => setDraft({})}
                  className="rounded-lg px-3 py-1.5 text-sm text-ink-muted hover:text-ink"
                >
                  {t('settings.discard')}
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
              </div>
            )}
          </div>
        </div>
      )}
    </AppShell>
  )
}

function Row({
  t,
  setting,
  draft,
  detected,
  onChange,
}: {
  t: Translate
  setting: Setting
  draft: string | null | undefined
  /** Modelos que devolvió el servidor local. Se ofrecen como sugerencias sin bloquear el campo:
   *  con Anthropic el nombre no sale de ninguna lista detectable. */
  detected?: string[]
  onChange: (value: string | null) => void
}) {
  const current = draft !== undefined ? draft : setting.value
  const input = 'w-full rounded-lg border border-line bg-canvas px-3 py-1.5 text-sm focus:border-accent'

  return (
    <div className="grid gap-3 p-4 sm:grid-cols-[1fr_16rem]">
      <div className="min-w-0">
        <p className="text-sm font-medium">
          {settingText(t, setting.key, 'label', setting.label)}
        </p>
        <p className="mt-0.5 text-xs text-ink-muted">
          {settingText(t, setting.key, 'help', setting.help)}
        </p>
        {setting.kind === 'Secret' && (
          <p className="mt-1 text-[11px] text-ink-subtle">
            {setting.isSet ? t('settings.secretDefined') : t('settings.secretUndefined')}
          </p>
        )}
      </div>

      <div className="sm:justify-self-end sm:w-64">
        {setting.kind === 'Boolean' ? (
          <select
            value={String(current ?? 'false')}
            onChange={(e) => onChange(e.target.value)}
            className={input}
          >
            <option value="true">{t('common.yes')}</option>
            <option value="false">{t('common.no')}</option>
          </select>
        ) : setting.kind === 'Select' ? (
          <select value={current ?? ''} onChange={(e) => onChange(e.target.value)} className={input}>
            {setting.options?.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        ) : setting.kind === 'Secret' ? (
          <input
            type="password"
            value={draft ?? ''}
            placeholder={setting.isSet ? '••••••••' : t('settings.undefinedShort')}
            onChange={(e) => onChange(e.target.value)}
            className={input}
          />
        ) : (
          <>
            <input
              type={setting.kind === 'Number' ? 'number' : 'text'}
              value={current ?? ''}
              onChange={(e) => onChange(e.target.value)}
              list={detected && detected.length > 0 ? `${setting.key}-detected` : undefined}
              className={input}
            />
            {detected && detected.length > 0 && (
              <datalist id={`${setting.key}-detected`}>
                {detected.map((m) => (
                  <option key={m} value={m} />
                ))}
              </datalist>
            )}
          </>
        )}
      </div>
    </div>
  )
}
