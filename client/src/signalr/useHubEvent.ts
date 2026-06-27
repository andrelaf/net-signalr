import { HubConnection } from '@microsoft/signalr'
import { useEffect } from 'react'

/**
 * Registra um handler para um evento server->client e o remove no cleanup.
 * Evita vazamento de handlers ao re-renderizar componentes.
 */
export function useHubEvent(
  conn: HubConnection | null,
  event: string,
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  handler: (...args: any[]) => void,
  deps: unknown[] = [],
) {
  useEffect(() => {
    if (!conn) return
    conn.on(event, handler)
    return () => conn.off(event, handler)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [conn, event, ...deps])
}
