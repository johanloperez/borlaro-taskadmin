import { useEffect, useState } from 'react'
import { getAuthToken } from '@/lib/api'

/** Una imagen dentro de una descripción en Markdown.
 *
 *  **El caso que hace falta resolver:** la captura que alguien adjuntó a la tarea y después
 *  referenció con `![](/api/items/…/adjuntos/…)`. Los adjuntos no son públicos —se sirven con
 *  sesión— y un `<img src>` no puede mandar el header de autorización, así que esa imagen se vería
 *  rota. Es el mismo problema que ya tenía el logo de la organización, y la misma salida: se trae
 *  con `fetch`, con el token en el header, y se convierte en una URL de blob.
 *
 *  Solo para lo que apunta a nuestra API. Una imagen externa se deja pasar tal cual: pedirla con
 *  nuestro token sería mandarle la credencial a un tercero. */
export function MarkdownImage({ src, alt }: { src?: string; alt?: string }) {
  const esNuestra = Boolean(src && (src.startsWith('/api/') || src.includes('/api/items/')))
  const [blob, setBlob] = useState<string | null>(null)
  const [falló, setFalló] = useState(false)

  useEffect(() => {
    if (!esNuestra || !src) return

    let cancelado = false
    let url: string | null = null

    async function traer() {
      try {
        const response = await fetch(src!, {
          headers: { Authorization: `Bearer ${getAuthToken()}` },
        })

        if (!response.ok) throw new Error(String(response.status))

        url = URL.createObjectURL(await response.blob())
        if (!cancelado) setBlob(url)
      } catch {
        if (!cancelado) setFalló(true)
      }
    }

    traer()

    return () => {
      cancelado = true
      // Sin esto, cada render deja un blob vivo en memoria hasta que se cierre la pestaña.
      if (url) URL.revokeObjectURL(url)
    }
  }, [src, esNuestra])

  if (falló) {
    return <span className="text-xs text-ink-subtle">[{alt || 'imagen'}]</span>
  }

  const real = esNuestra ? blob : src
  if (!real) return null

  return (
    <img
      src={real}
      alt={alt ?? ''}
      // Nunca más ancha que su contenedor: una captura de 3000 px reventaría el ancho del panel
      // y aparecería una barra horizontal en toda la descripción.
      className="my-2 max-w-full rounded-lg border border-line"
      loading="lazy"
    />
  )
}
