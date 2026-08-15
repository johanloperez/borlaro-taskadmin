import { Languages } from 'lucide-react'
import { cn } from '@/lib/cn'
import { LANGUAGES, LANGUAGE_LABEL, useI18n } from '@/lib/i18n'

/** El selector de idioma.
 *
 *  Es un `select` nativo y no un menú propio a propósito: son tres opciones, aparece una vez cada
 *  varios meses, y el control del sistema ya sabe abrirse con el teclado, leerse con un lector de
 *  pantalla y comportarse bien en un teléfono. */
export function LanguageSwitch({ compact = false }: { compact?: boolean }) {
  const { language, setLanguage } = useI18n()

  return (
    <label
      className={cn(
        'inline-flex items-center gap-1.5 text-ink-muted',
        compact ? 'text-[13px]' : 'text-sm',
      )}
    >
      <Languages className="size-4" aria-hidden />
      <span className="sr-only">{LANGUAGE_LABEL[language]}</span>
      <select
        value={language}
        onChange={(e) => setLanguage(e.target.value as (typeof LANGUAGES)[number])}
        className="cursor-pointer rounded-md border border-transparent bg-transparent py-0.5 pr-1
                   hover:border-line focus:border-accent focus:outline-none"
      >
        {LANGUAGES.map((code) => (
          <option key={code} value={code}>
            {LANGUAGE_LABEL[code]}
          </option>
        ))}
      </select>
    </label>
  )
}
