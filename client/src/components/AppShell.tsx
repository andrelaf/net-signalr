import { HubConnectionState } from '@microsoft/signalr'
import { useState } from 'react'
import { Link, NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { useWorkspaceHub } from '../signalr/WorkspaceHubContext'
import { useHubEvent } from '../signalr/useHubEvent'
import type { DirectMessageDto, NotificationDto, PresenceDto } from '../types'

interface Toast {
  id: string
  title: string
  body: string
}

const STATE_LABEL: Record<HubConnectionState, string> = {
  [HubConnectionState.Connected]: 'Conectado',
  [HubConnectionState.Connecting]: 'Conectando…',
  [HubConnectionState.Reconnecting]: 'Reconectando…',
  [HubConnectionState.Disconnected]: 'Desconectado',
  [HubConnectionState.Disconnecting]: 'Desconectando…',
}

export function AppShell() {
  const { user, logout } = useAuth()
  const { conn, state } = useWorkspaceHub()
  const [online, setOnline] = useState<Map<string, PresenceDto>>(new Map())
  const [toasts, setToasts] = useState<Toast[]>([])

  const pushToast = (title: string, body: string) => {
    const id = crypto.randomUUID()
    setToasts((t) => [...t, { id, title, body }])
    setTimeout(() => setToasts((t) => t.filter((x) => x.id !== id)), 6000)
  }

  // Presença: o servidor envia o estado atual ao conectar e mudanças depois.
  useHubEvent(conn, 'PresenceChanged', (p: PresenceDto) => {
    setOnline((prev) => {
      const next = new Map(prev)
      if (p.online) next.set(p.userId, p)
      else next.delete(p.userId)
      return next
    })
  })

  // Notificações push (ex.: SLA do BackgroundService, status de ticket via controller).
  useHubEvent(conn, 'ReceiveNotification', (n: NotificationDto) =>
    pushToast(`🔔 ${n.type}`, n.payload),
  )

  // Mensagens diretas (Clients.User -> IUserIdProvider).
  useHubEvent(conn, 'ReceiveDirectMessage', (dm: DirectMessageDto) =>
    pushToast(`✉️ DM de ${dm.fromDisplayName}`, dm.content),
  )

  const isStaff = user?.role === 'Agent' || user?.role === 'Manager'
  const connClass =
    state === HubConnectionState.Connected
      ? 'ok'
      : state === HubConnectionState.Reconnecting
        ? 'warn'
        : 'bad'

  return (
    <div className="shell">
      <header className="topbar">
        <Link to="/" className="brand">
          🎧 Help Desk
        </Link>
        <nav>
          <NavLink to="/tickets">Tickets</NavLink>
          {user?.role === 'Manager' && <NavLink to="/dashboard">Dashboard</NavLink>}
        </nav>
        <div className="topbar-right">
          <span className={`conn ${connClass}`}>● {STATE_LABEL[state]}</span>
          <span className="muted">
            {user?.displayName} ({user?.role})
          </span>
          <button className="link" onClick={logout}>
            Sair
          </button>
        </div>
      </header>

      <div className="body">
        <main className="content">
          <Outlet context={{ online }} />
        </main>

        <aside className="sidebar">
          <h3>Online ({online.size})</h3>
          <ul className="presence-list">
            {[...online.values()].map((p) => (
              <li key={p.userId}>
                <span className="dot" /> {p.displayName}
                {p.userId === user?.userId && ' (você)'}
              </li>
            ))}
            {online.size === 0 && <li className="muted">ninguém online</li>}
          </ul>
          {isStaff && (
            <p className="hint muted">
              Dica: abra outra aba com outro usuário para ver presença, chat e DMs em tempo real.
            </p>
          )}
        </aside>
      </div>

      <div className="toasts">
        {toasts.map((t) => (
          <div className="toast" key={t.id}>
            <strong>{t.title}</strong>
            <div>{t.body}</div>
          </div>
        ))}
      </div>
    </div>
  )
}
