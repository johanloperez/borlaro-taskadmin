/** Zonas horarias para elegir de una lista, en vez de escribir el id a mano. Un `America/Bs_As`
 *  mal tipeado no lo rechaza nadie hasta que el backend contesta 400 —y si llegara a pasar, la
 *  persona queda sin check-in a la hora que espera—, así que la UI no ofrece la posibilidad.
 *
 *  Los ids salen del navegador (`Intl.supportedValuesOf`), que da la base IANA: los mismos ids
 *  que ya guarda la columna y que valida el backend. */

/** Para navegadores sin `Intl.supportedValuesOf`. No pretende ser completa: alcanza para que la
 *  lista nunca quede vacía y para cubrir dónde está el equipo. */
const FALLBACK_ZONE_IDS = [
  'UTC',
  'America/Argentina/Buenos_Aires',
  'America/Santiago',
  'America/Montevideo',
  'America/Sao_Paulo',
  'America/Asuncion',
  'America/La_Paz',
  'America/Lima',
  'America/Bogota',
  'America/Caracas',
  'America/Mexico_City',
  'America/New_York',
  'America/Chicago',
  'America/Denver',
  'America/Los_Angeles',
  'Europe/Madrid',
  'Europe/London',
  'Europe/Berlin',
  'Europe/Lisbon',
  'Asia/Jerusalem',
  'Asia/Kolkata',
  'Asia/Tokyo',
  'Australia/Sydney',
]

const REGION_LABELS: Record<string, string> = {
  Africa: 'África',
  America: 'América',
  Antarctica: 'Antártida',
  Arctic: 'Ártico',
  Asia: 'Asia',
  Atlantic: 'Atlántico',
  Australia: 'Australia',
  Europe: 'Europa',
  Indian: 'Índico',
  Pacific: 'Pacífico',
}

export interface TimeZoneOption {
  id: string
  /** Ciudad y desplazamiento actual: «Buenos Aires · GMT-3». */
  label: string
}

export interface TimeZoneGroup {
  region: string
  zones: TimeZoneOption[]
}

function zoneIds(): string[] {
  if (typeof Intl.supportedValuesOf === 'function') {
    const ids = Intl.supportedValuesOf('timeZone')
    // El navegador no siempre incluye UTC en la lista, y es la zona por defecto del backend.
    return ids.includes('UTC') ? ids : ['UTC', ...ids]
  }
  return FALLBACK_ZONE_IDS
}

/** «GMT-3» para la zona, o null si el navegador no la conoce. */
function offsetLabel(id: string): string | null {
  try {
    const parts = new Intl.DateTimeFormat('es', { timeZone: id, timeZoneName: 'shortOffset' })
      .formatToParts(new Date())
    return parts.find((p) => p.type === 'timeZoneName')?.value ?? null
  } catch {
    return null
  }
}

/** «America/Argentina/Buenos_Aires» → «Buenos Aires (Argentina)». */
function cityLabel(id: string): string {
  const [, ...rest] = id.split('/')
  if (rest.length === 0) return id.replace(/_/g, ' ')
  const city = rest[rest.length - 1].replace(/_/g, ' ')
  // Los ids de tres tramos meten el país en el medio: «Argentina/Buenos_Aires».
  const middle = rest.slice(0, -1).join(' / ').replace(/_/g, ' ')
  return middle ? `${city} (${middle})` : city
}

let cached: TimeZoneGroup[] | null = null

/** Zonas agrupadas por región, ordenadas alfabéticamente. Se calcula una sola vez por carga:
 *  los desplazamientos cambian con el horario de verano, no mientras alguien llena un formulario. */
export function timeZoneGroups(): TimeZoneGroup[] {
  if (cached) return cached

  const byRegion = new Map<string, TimeZoneOption[]>()
  for (const id of zoneIds()) {
    const prefix = id.split('/')[0]
    const region = REGION_LABELS[prefix] ?? 'Otras'
    const offset = offsetLabel(id)
    const zones = byRegion.get(region) ?? []
    zones.push({ id, label: offset ? `${cityLabel(id)} · ${offset}` : cityLabel(id) })
    byRegion.set(region, zones)
  }

  cached = [...byRegion.entries()]
    .map(([region, zones]) => ({
      region,
      zones: zones.sort((a, b) => a.label.localeCompare(b.label, 'es')),
    }))
    // «Otras» (UTC y los alias sueltos) al final: no es lo que busca nadie.
    .sort((a, b) =>
      a.region === 'Otras' ? 1 : b.region === 'Otras' ? -1 : a.region.localeCompare(b.region, 'es'),
    )

  return cached
}

/** Valor por defecto del alta: quien crea la cuenta suele estar en la misma zona que el equipo.
 *  Se devuelve tal cual la reporta el navegador aunque no figure en la lista —hay builds de ICU
 *  que enumeran el alias («America/Buenos_Aires») y no el id canónico, o al revés—: es un id IANA
 *  válido igual, y el select lo ofrece como primera opción. Cambiarlo por UTC sería elegir mal
 *  por su cuenta la única cosa que este campo tiene que acertar. */
export function browserTimeZone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone
}

/** Si el id está entre los que ofrece la lista. Un `false` no quiere decir inválido: puede ser
 *  el alias de una zona que este navegador enumera con otro nombre. */
export function knownTimeZone(id: string): boolean {
  return timeZoneGroups().some((g) => g.zones.some((z) => z.id === id))
}
