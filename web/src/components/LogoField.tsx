import { useEffect, useRef, useState } from 'react'
import { ImageUp, X } from 'lucide-react'
import { useT } from '@/lib/i18n'

/** El selector de logo que comparten las tres pantallas que crean una organización.
 *
 *  La vista previa es local —una URL de objeto del archivo elegido— y no necesita al servidor:
 *  recién al enviar el formulario se sube. El estado del archivo lo guarda el padre, que es
 *  quien arma el `FormData`. */
export function LogoField({
  logo,
  onLogo,
  error,
}: {
  logo: File | null
  onLogo: (file: File | null) => void
  error?: string | null
}) {
  const t = useT()
  const input = useRef<HTMLInputElement>(null)
  const [preview, setPreview] = useState<string | null>(null)

  // La vista previa es del archivo que eligió; se limpia si lo quitan.
  useEffect(() => {
    if (!logo) {
      setPreview(null)
      return
    }
    const url = URL.createObjectURL(logo)
    setPreview(url)
    return () => URL.revokeObjectURL(url)
  }, [logo])

  return (
    <div className="space-y-1.5">
      <span className="block text-sm text-ink-muted">{t('logo.label')}</span>

      <div className="flex items-center gap-3">
        <div className="grid size-12 shrink-0 place-items-center overflow-hidden rounded-lg border border-line bg-canvas">
          {preview ? (
            <img src={preview} alt="" className="size-full object-contain" />
          ) : (
            <ImageUp className="size-5 text-ink-subtle" />
          )}
        </div>

        <input
          ref={input}
          type="file"
          accept="image/png,image/jpeg,image/gif,image/webp,image/svg+xml"
          className="hidden"
          onChange={(e) => {
            onLogo(e.target.files?.[0] ?? null)
            e.target.value = ''
          }}
        />

        <div className="flex flex-col gap-1">
          <div className="flex gap-2">
            <button
              type="button"
              onClick={() => input.current?.click()}
              className="rounded-md border border-line px-2.5 py-1 text-xs text-ink-muted hover:bg-canvas hover:text-ink"
            >
              {t('logo.change')}
            </button>
            {logo && (
              <button
                type="button"
                onClick={() => onLogo(null)}
                className="inline-flex items-center gap-1 rounded-md border border-line px-2.5 py-1 text-xs text-ink-muted hover:bg-canvas hover:text-ink"
              >
                <X className="size-3" />
                {t('logo.remove')}
              </button>
            )}
          </div>
          <p className="text-[11px] text-ink-subtle">{t('logo.help')}</p>
        </div>
      </div>

      {error && <p className="text-[11px] text-stage-blocked">{error}</p>}
    </div>
  )
}
