import { useEffect, useRef, useState } from 'react'
import { useOutletContext, useParams } from 'react-router-dom'
import { api } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { useWorkspaceHub } from '../signalr/WorkspaceHubContext'
import { useHubEvent } from '../signalr/useHubEvent'
import { uploadFile } from '../signalr/uploads'
import type { MessageDto, PresenceDto, TicketDto, TypingDto } from '../types'

type ShellCtx = { online: Map<string, PresenceDto> }

export function ChatPage() {
  const { id = '' } = useParams()
  const { user } = useAuth()
  const { conn } = useWorkspaceHub()
  const { online } = useOutletContext<ShellCtx>()

  const [ticket, setTicket] = useState<TicketDto | null>(null)
  const [messages, setMessages] = useState<MessageDto[]>([])
  const [draft, setDraft] = useState('')
  const [typers, setTypers] = useState<Map<string, string>>(new Map())
  const [progress, setProgress] = useState<number | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [dmTarget, setDmTarget] = useState('')
  const [dmText, setDmText] = useState('')

  const typingTimer = useRef<number | null>(null)
  const isStaff = user?.role === 'Agent' || user?.role === 'Manager'

  // Carrega ticket + histórico (REST) e entra no grupo do ticket (SignalR).
  useEffect(() => {
    let joined = false
    setMessages([])
    api.getTicket(id).then(setTicket).catch((e) => setError((e as Error).message))
    api
      .getMessages(id)
      .then((page) => setMessages(page.items))
      .catch((e) => setError((e as Error).message))

    if (conn) {
      conn
        .invoke('JoinTicket', id)
        .then(() => {
          joined = true
        })
        .catch((e) => setError(String(e)))
    }
    return () => {
      if (conn && joined) conn.invoke('LeaveTicket', id).catch(() => {})
    }
  }, [id, conn])

  const upsert = (m: MessageDto) =>
    setMessages((prev) => (prev.some((x) => x.id === m.id) ? prev : [...prev, m]))

  useHubEvent(conn, 'ReceiveMessage', (m: MessageDto) => {
    if (m.ticketId === id) upsert(m)
  }, [id])

  useHubEvent(conn, 'TicketUpdated', (t: TicketDto) => {
    if (t.id === id) setTicket(t)
  }, [id])

  useHubEvent(conn, 'TypingChanged', (t: TypingDto) => {
    if (t.ticketId !== id || t.userId === user?.userId) return
    setTypers((prev) => {
      const next = new Map(prev)
      if (t.isTyping) next.set(t.userId, t.displayName)
      else next.delete(t.userId)
      return next
    })
  }, [id])

  useHubEvent(conn, 'UploadProgress', (_uploadId: string, percent: number) => {
    setProgress(percent >= 100 ? null : percent)
  })

  const send = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!draft.trim() || !conn) return
    try {
      await conn.invoke('SendMessage', id, draft.trim())
      setDraft('')
      await conn.invoke('Typing', id, false)
    } catch (err) {
      setError(String(err))
    }
  }

  const onDraftChange = (value: string) => {
    setDraft(value)
    if (!conn) return
    conn.invoke('Typing', id, true).catch(() => {})
    if (typingTimer.current) window.clearTimeout(typingTimer.current)
    typingTimer.current = window.setTimeout(() => {
      conn.invoke('Typing', id, false).catch(() => {})
    }, 1500)
  }

  const onFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file || !conn) return
    setProgress(0)
    try {
      await uploadFile(conn, id, file)
    } catch (err) {
      setError(String(err))
    } finally {
      setProgress(null)
      e.target.value = ''
    }
  }

  const resolve = async () => {
    if (!conn || !ticket) return
    try {
      await conn.invoke('ResolveTicket', id, ticket.rowVersion)
    } catch (err) {
      setError(String(err)) // pode ser conflito de concorrência otimista
    }
  }

  const assignToMe = async () => {
    if (!conn || !user) return
    try {
      await conn.invoke('AssignTicket', id, user.userId)
    } catch (err) {
      setError(String(err))
    }
  }

  const requestClose = async () => {
    if (!conn) return
    try {
      const confirmed = await conn.invoke<boolean>('RequestCloseConfirmation', id)
      setError(confirmed ? null : 'Cliente recusou o encerramento.')
    } catch (err) {
      setError(String(err))
    }
  }

  const sendDm = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!conn || !dmTarget || !dmText.trim()) return
    await conn.invoke('SendDirectMessage', dmTarget, dmText.trim())
    setDmText('')
  }

  return (
    <div className="chat">
      <div className="chat-header">
        <div>
          <h2>{ticket?.subject ?? 'Ticket'}</h2>
          {ticket && (
            <span className={`status ${ticket.status.toLowerCase()}`}>{ticket.status}</span>
          )}
        </div>
        {isStaff && ticket && (
          <div className="actions">
            <button onClick={assignToMe}>Atribuir a mim</button>
            <button onClick={resolve}>Resolver</button>
            <button onClick={requestClose} title="Client result: pede confirmação ao cliente">
              Pedir confirmação
            </button>
          </div>
        )}
      </div>

      {error && <div className="error">{error}</div>}

      <div className="messages">
        {messages.map((m) => (
          <div
            key={m.id}
            className={`msg ${m.senderId === user?.userId ? 'mine' : ''} ${
              m.kind === 'System' ? 'system' : ''
            }`}
          >
            <div className="msg-meta">
              <strong>{m.senderName}</strong>{' '}
              <span className="muted">{new Date(m.sentAt).toLocaleTimeString()}</span>
            </div>
            {m.kind === 'File' && m.attachment ? (
              <div className="file">📎 {m.attachment.fileName} ({Math.round(m.attachment.sizeBytes / 1024)} KB)</div>
            ) : (
              <div className="msg-body">{m.content}</div>
            )}
          </div>
        ))}
      </div>

      {typers.size > 0 && (
        <div className="typing muted">{[...typers.values()].join(', ')} digitando…</div>
      )}

      {progress !== null && (
        <div className="progress">
          <div className="progress-bar" style={{ width: `${progress}%` }} />
          <span>{progress}%</span>
        </div>
      )}

      <form className="composer" onSubmit={send}>
        <input
          placeholder="Escreva uma mensagem…"
          value={draft}
          onChange={(e) => onDraftChange(e.target.value)}
        />
        <label className="file-btn">
          📎
          <input type="file" hidden onChange={onFile} />
        </label>
        <button type="submit">Enviar</button>
      </form>

      <details className="dm">
        <summary>Mensagem direta (Clients.User)</summary>
        <form className="row" onSubmit={sendDm}>
          <select value={dmTarget} onChange={(e) => setDmTarget(e.target.value)}>
            <option value="">— destinatário online —</option>
            {[...online.values()]
              .filter((p) => p.userId !== user?.userId)
              .map((p) => (
                <option key={p.userId} value={p.userId}>
                  {p.displayName}
                </option>
              ))}
          </select>
          <input
            placeholder="mensagem privada…"
            value={dmText}
            onChange={(e) => setDmText(e.target.value)}
          />
          <button type="submit">Enviar DM</button>
        </form>
      </details>
    </div>
  )
}
