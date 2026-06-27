import { HubConnectionBuilder, LogLevel, type ISubscription } from '@microsoft/signalr'
import { useEffect, useRef, useState } from 'react'
import { tokenStore } from '../api/client'
import type { DashboardMetricsDto } from '../types'

/**
 * Consome o stream SERVIDOR -> CLIENTE do DashboardHub (restrito a Manager).
 * Usa uma conexão dedicada e connection.stream(...).subscribe(...).
 * Ao desmontar, cancela a assinatura -> o CancellationToken do servidor é acionado.
 */
export function DashboardPage() {
  const [metrics, setMetrics] = useState<DashboardMetricsDto | null>(null)
  const [history, setHistory] = useState<DashboardMetricsDto[]>([])
  const [error, setError] = useState<string | null>(null)
  const subRef = useRef<ISubscription<DashboardMetricsDto> | null>(null)

  useEffect(() => {
    const conn = new HubConnectionBuilder()
      .withUrl('/hubs/dashboard', { accessTokenFactory: () => tokenStore.get() ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    let active = true
    conn
      .start()
      .then(() => {
        if (!active) return
        subRef.current = conn.stream<DashboardMetricsDto>('StreamMetrics', 2).subscribe({
          next: (m) => {
            setMetrics(m)
            setHistory((h) => [...h.slice(-19), m])
          },
          complete: () => {},
          error: (err) => setError(String(err)),
        })
      })
      .catch((err) => setError(String(err)))

    return () => {
      active = false
      subRef.current?.dispose()
      conn.stop()
    }
  }, [])

  return (
    <div>
      <h2>Dashboard ao vivo</h2>
      <p className="muted">Streaming servidor → cliente (IAsyncEnumerable), atualizando a cada 2s.</p>
      {error && <div className="error">{error}</div>}

      {metrics && (
        <div className="kpis">
          <Kpi label="Abertos" value={metrics.openTickets} cls="open" />
          <Kpi label="Pendentes" value={metrics.pendingTickets} cls="pending" />
          <Kpi label="Resolvidos" value={metrics.resolvedTickets} cls="resolved" />
          <Kpi label="Online" value={metrics.onlineUsers} cls="" />
          <Kpi label="Msgs/1h" value={metrics.messagesLastHour} cls="" />
        </div>
      )}

      <h3>Mensagens na última hora (série)</h3>
      <div className="spark">
        {history.map((m, i) => (
          <div
            key={i}
            className="bar"
            style={{ height: `${Math.min(100, m.messagesLastHour * 10 + 4)}px` }}
            title={`${m.messagesLastHour} msgs @ ${new Date(m.timestamp).toLocaleTimeString()}`}
          />
        ))}
      </div>
    </div>
  )
}

function Kpi({ label, value, cls }: { label: string; value: number; cls: string }) {
  return (
    <div className={`kpi ${cls}`}>
      <div className="kpi-value">{value}</div>
      <div className="kpi-label">{label}</div>
    </div>
  )
}
