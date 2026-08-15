import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api } from '@/lib/api'
import { en } from '@/locales/en'
import { es } from '@/locales/es'
import { pt } from '@/locales/pt'

export const LANGUAGES = ['es', 'en', 'pt'] as const
export type Language = (typeof LANGUAGES)[number]

/** Cómo se llama cada idioma **en ese idioma**. Un selector que dice «Spanish, English,
 *  Portuguese» en inglés es inútil justo para quien no lee inglés. */
export const LANGUAGE_LABEL: Record<Language, string> = {
  es: 'Español',
  en: 'English',
  pt: 'Português',
}

/** El inglés es el diccionario de referencia y el idioma de reserva: define las claves que los
 *  otros dos tienen que tener, y es a lo que se cae cuando algo falla. Si mañana falta una
 *  traducción, TypeScript lo dice al compilar y no el usuario al encontrarse una clave cruda en
 *  pantalla. */
export type Dictionary = typeof en

const DICTIONARIES: Record<Language, Dictionary> = { es, en, pt }

const STORAGE_KEY = 'taskadmin.lang'

/** El idioma que corresponde antes de saber quién entró.
 *
 *  El orden es deliberado: lo que la persona eligió explícitamente vale más que lo que dice su
 *  navegador, porque quien tiene Windows en inglés y trabaja en portugués no debería tener que
 *  pelearse con la detección todos los días. */
export function detectLanguage(): Language {
  const stored = localStorage.getItem(STORAGE_KEY)
  if (isLanguage(stored)) return stored

  for (const candidate of navigator.languages ?? [navigator.language]) {
    // «pt-BR» y «pt-PT» son los dos portugués para lo que nos importa acá.
    const base = candidate.slice(0, 2).toLowerCase()
    if (isLanguage(base)) return base
  }

  // Inglés como reserva: es el idioma que más gente entiende de los tres, y el que menos deja
  // afuera a quien llega con un navegador configurado en algo que no hablamos.
  return 'en'
}

function isLanguage(value: string | null | undefined): value is Language {
  return value !== null && value !== undefined && (LANGUAGES as readonly string[]).includes(value)
}

interface I18nValue {
  language: Language
  setLanguage: (language: Language) => void
  t: Translate
}

/** Traduce una clave. El segundo argumento reemplaza los `{marcadores}` del texto.
 *
 *  Los marcadores van con nombre y no por posición porque el orden de las palabras cambia entre
 *  idiomas: «3 tareas de Ana» y «Ana's 3 tasks» no ponen los datos en el mismo lugar. */
export type Translate = (key: keyof Dictionary, values?: Record<string, string | number>) => string

const I18nContext = createContext<I18nValue | null>(null)

export function I18nProvider({ children }: { children: ReactNode }) {
  const [language, setLanguageState] = useState<Language>(detectLanguage)

  useEffect(() => {
    document.documentElement.lang = language
  }, [language])

  // Al entrar, el idioma guardado en el usuario pisa al del navegador. El aviso llega por evento
  // porque quien lo aplica es el store de sesión, que no puede llamar a un hook: es un módulo,
  // no un componente.
  useEffect(() => {
    const onChange = (event: Event) => {
      const next = (event as CustomEvent<Language>).detail
      if (isLanguage(next)) setLanguageState(next)
    }

    window.addEventListener(LANGUAGE_EVENT, onChange)
    return () => window.removeEventListener(LANGUAGE_EVENT, onChange)
  }, [])

  const setLanguage = useCallback((next: Language) => {
    setLanguageState(next)
    localStorage.setItem(STORAGE_KEY, next)

    // Se guarda también en el servidor, y sin esperar la respuesta: es lo que hace que los emails
    // de la escalera lleguen en el idioma correcto, que se arman sin navegador del cual deducirlo.
    // Si falla —por ejemplo porque todavía no hay sesión— la elección local sigue valiendo.
    void api('/api/auth/me/idioma', { method: 'PUT', body: { language: next } }).catch(() => {})
  }, [])

  const value = useMemo<I18nValue>(() => {
    const dictionary = DICTIONARIES[language]

    const t: Translate = (key, values) => {
      // Si a un idioma le falta una clave, cae al inglés en vez de mostrar la clave cruda: una
      // frase en otro idioma se entiende, «settings.model.title» no.
      const template = dictionary[key] ?? en[key] ?? String(key)

      if (!values) return template

      return template.replace(/\{(\w+)\}/g, (match, name: string) =>
        name in values ? String(values[name]) : match,
      )
    }

    return { language, setLanguage, t }
  }, [language, setLanguage])

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>
}

export function useI18n(): I18nValue {
  const value = useContext(I18nContext)
  if (!value) throw new Error('useI18n fuera de I18nProvider')
  return value
}

/** El atajo de todos los días: `const t = useT()`. */
export function useT(): Translate {
  return useI18n().t
}

/** El locale para fechas y números.
 *
 *  No alcanza con el idioma: «11/8» es el 11 de agosto para quien lee español o portugués, y el 8
 *  de noviembre para quien lee inglés. Un `toLocaleDateString()` sin locale usa el del navegador,
 *  que puede no ser el idioma que la persona eligió acá. */
export const LOCALE: Record<Language, string> = {
  es: 'es-AR',
  en: 'en-US',
  pt: 'pt-BR',
}

export function useLocale(): string {
  return LOCALE[useI18n().language]
}

/** Sincroniza el idioma guardado en el usuario con el de la sesión del navegador.
 *
 *  Se llama al entrar: si la persona ya había elegido uno, ese manda por encima de lo que diga el
 *  navegador de la máquina en la que está entrando hoy. */
export function applyUserLanguage(language: string | null | undefined) {
  if (!isLanguage(language)) return
  localStorage.setItem(STORAGE_KEY, language)
  window.dispatchEvent(new CustomEvent(LANGUAGE_EVENT, { detail: language }))
}

const LANGUAGE_EVENT = 'taskadmin:language'
