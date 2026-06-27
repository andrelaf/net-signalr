import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { useWorkspaceHub } from '../signalr/WorkspaceHubContext'
import { useHubEvent } from '../signalr/useHubEvent'
import type { TicketDto } from '../types'

export function TicketsPage() {
  const { user } = useAuth()
  const { conn } = useWorkspaceHub()
  const [tickets, setTickets] = useState<TicketDto[]>([])
  const [subject, setSubject] = useState('')
  const [error, setError] = useState<string | null>(null)

  const isCustomer = user?.role === 'Customer'

  useEffect(() => {
    api.listTickets().then(setTickets).catch((e) => setError((e as Error).message))
  }, [])

  // Atualização em tempo real: novos tickets / mudança de status chegam via TicketUpdated.
  useHubEvent(conn, 'TicketUpdated', (t: TicketDto) => {
    setTickets((prev) => {
      const idx = prev.findIndex((x) => x.id === t.id)
      if (idx === -1) return [t, ...prev]
      const next = [...prev]
      next[idx] = t
      return next
    })
  })

  const create = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!subject.trim()) return
    try {
      await api.createTicket(subject.trim())
      setSubject('')
    } catch (err) {
      setError((err as Error).message)
    }
  }

  return (
    <div>
      <h2>Tickets</h2>
      {error && <div className="error">{error}</div>}

      {isCustomer && (
        <form className="row" onSubmit={create}>
          <input
            placeholder="Descreva seu problema…"
            value={subject}
            onChange={(e) => setSubject(e.target.value)}
          />
          <button type="submit">Abrir ticket</button>
        </form>
      )}

      <table className="tickets">
        <thead>
          <tr>
            <th>Assunto</th>
            <th>Status</th>
            <th>Cliente</th>
            <th>Agente</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {tickets.map((t) => (
            <tr key={t.id}>
              <td>{t.subject}</td>
              <td>
                <span className={`status ${t.status.toLowerCase()}`}>{t.status}</span>
              </td>
              <td>{t.customerName}</td>
              <td>{t.assignedAgentName ?? '—'}</td>
              <td>
                <Link to={`/tickets/${t.id}`}>Abrir</Link>
              </td>
            </tr>
          ))}
          {tickets.length === 0 && (
            <tr>
              <td colSpan={5} className="muted">
                Nenhum ticket.
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  )
}
