import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { getAuthToken } from '@/lib/api'
import { keys } from '@/lib/queries'

/** Lo que el servidor manda cuando algo se movió en un tablero. */
export interface BoardEvent {
  projectKey: string
  itemId: string
  itemKey: string
  kind: 'created' | 'updated' | 'moved' | 'deleted'
  byAgent: boolean
}

/**
 * Una sola conexión para toda la aplicación.
 *
 * Un `HubConnection` por pantalla significaría abrir y cerrar un WebSocket en cada navegación, y
 * varias conexiones vivas cuando hay más de un componente escuchando. Se comparte una y cada
 * pantalla se suscribe al tablero que está mirando.
 */
let connection: HubConnection | null = null
let starting: Promise<void> | null = null

/** Cuántas pantallas miran cada tablero. Sin esto, desmontar una de dos vistas del mismo
 *  proyecto dejaría muda a la otra. */
const watchers = new Map<string, number>()

function ensure(): HubConnection {
  if (connection) return connection

  connection = new HubConnectionBuilder()
    .withUrl(`/hubs/agent?access_token=${encodeURIComponent(getAuthToken() ?? '')}`)
    // Reintenta para siempre con espera creciente. Que se caiga la red no puede dejar la
    // pantalla congelada en un estado viejo sin decirlo.
    .withAutomaticReconnect([0, 2000, 10000, 30000, 60000])
    .configureLogging(LogLevel.Warning)
    .build()

  // Al reconectar hay que volver a pedir los grupos: el servidor no recuerda las suscripciones
  // de una conexión que se murió. Sin esto, después de un corte de red la pantalla queda
  // conectada y muda, que es peor que estar desconectada porque no se nota.
  connection.onreconnected(() => {
    for (const key of watchers.keys()) {
      void connection?.invoke('WatchBoard', key)
    }
  })

  return connection
}

async function start(): Promise<void> {
  const hub = ensure()
  if (hub.state === HubConnectionState.Connected) return
  starting ??= hub.start().finally(() => { starting = null })
  return starting
}

/**
 * Mantiene el tablero al día sin refrescar.
 *
 * Cuando algo cambia, se invalida la consulta en vez de aplicar el cambio a mano sobre el estado
 * local. Es a propósito: el evento dice *qué pasó*, no *cómo quedó*, así que reconstruir el nuevo
 * estado en el cliente sería reimplementar las reglas del servidor —qué columna, qué orden, qué
 * permisos— y mantener las dos copias iguales para siempre. Invalidar delega eso en la API, que
 * ya lo sabe hacer y ya aplica los permisos de quien mira.
 */
export function useBoardRealtime(projectKey: string | undefined) {
  const qc = useQueryClient()

  useEffect(() => {
    if (!projectKey || !getAuthToken()) return

    let alive = true

    const onBoard = (evento: BoardEvent) => {
      if (evento.projectKey.toUpperCase() !== projectKey.toUpperCase()) return

      void qc.invalidateQueries({ queryKey: keys.board(projectKey) })
      void qc.invalidateQueries({ queryKey: keys.project(projectKey) })
      void qc.invalidateQueries({ queryKey: keys.mine })

      // El historial de la tarea que cambió, por si hay un panel de detalle abierto sobre ella.
      void qc.invalidateQueries({ queryKey: keys.events(evento.itemId) })
    }

    void (async () => {
      try {
        await start()
        if (!alive) return

        connection?.on('board', onBoard)

        watchers.set(projectKey, (watchers.get(projectKey) ?? 0) + 1)
        await connection?.invoke('WatchBoard', projectKey)
      } catch {
        // Sin canal en vivo la aplicación sigue andando: se ve el estado de la última carga y
        // se actualiza al navegar. Es una degradación, no una falla.
      }
    })()

    return () => {
      alive = false
      connection?.off('board', onBoard)

      const quedan = (watchers.get(projectKey) ?? 1) - 1
      if (quedan <= 0) {
        watchers.delete(projectKey)
        void connection?.invoke('UnwatchBoard', projectKey).catch(() => {})
      } else {
        watchers.set(projectKey, quedan)
      }
    }
  }, [projectKey, qc])
}

/** Corta el canal al cerrar sesión: si no, seguiría vivo con el token de quien se fue. */
export function stopRealtime() {
  watchers.clear()
  void connection?.stop().catch(() => {})
  connection = null
}
