import { useEffect, useState } from 'react'
import { getAuthToken } from '@/lib/api'
import { cn } from '@/lib/cn'

/** El logo de una organización, traído con la sesión.
 *
 *  El `<img>` no puede mandar el header de autorización, así que esto lo trae con fetch —con el
 *  token en el header— y lo convierte en una URL de blob. Sin logo, o si la descarga falla, no se
 *  dibuja nada: la organización se sigue mostrando por su nombre.
 *
 *  `hasLogo` es la ruta que el servidor ya dijo que existe: sirve para no pedir el logo de
 *  organizaciones que no tienen uno (el pedido igual devolvería 404). */
export function OrgLogo({
  organizationId,
  hasLogo,
  className,
  name,
}: {
  organizationId: string
  hasLogo?: boolean
  className?: string
  /** Con nombre, una organización sin logo se dibuja con su inicial en vez de no dibujarse. Hace
   *  falta desde que el logo pasó a ser la marca principal del encabezado: sin algo que ocupe ese
   *  lugar, la cabecera de quien no cargó logo se ve rota en vez de sobria. */
  name?: string
}) {
  const [src, setSrc] = useState<string | null>(null)

  useEffect(() => {
    if (!hasLogo) {
      setSrc(null)
      return
    }

    let cancelled = false
    let objectUrl: string | null = null

    async function load() {
      try {
        const token = getAuthToken()
        const response = await fetch(`/api/organizations/${organizationId}/logo`, {
          headers: token ? { Authorization: `Bearer ${token}` } : {},
        })
        if (!response.ok) return
        const blob = await response.blob()
        if (cancelled) return
        objectUrl = URL.createObjectURL(blob)
        setSrc(objectUrl)
      } catch {
        // Sin logo no se dibuja nada.
      }
    }

    void load()

    return () => {
      cancelled = true
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [organizationId, hasLogo])

  if (!src) {
    if (!name) return null

    // La inicial sobre un fondo neutro: ocupa el mismo lugar que ocuparía el logo, así el
    // encabezado no cambia de forma según quién haya cargado uno.
    return (
      <span
        aria-hidden
        className={cn(
          'inline-flex items-center justify-center bg-canvas font-semibold text-ink-muted',
          className,
        )}
      >
        {name.trim().charAt(0).toUpperCase()}
      </span>
    )
  }

  return <img src={src} alt="" aria-hidden className={className} />
}
