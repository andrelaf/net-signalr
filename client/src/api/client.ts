import type { LoginResponse, MessagePageResponse, TicketDto } from '../types'

const TOKEN_KEY = 'signalrdemo.token'

export const tokenStore = {
  get: () => localStorage.getItem(TOKEN_KEY),
  set: (t: string) => localStorage.setItem(TOKEN_KEY, t),
  clear: () => localStorage.removeItem(TOKEN_KEY),
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = tokenStore.get()
  const res = await fetch(path, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(options.headers ?? {}),
    },
  })
  if (!res.ok) {
    let message = `Erro ${res.status}`
    try {
      const body = await res.json()
      message = body.error ?? message
    } catch {
      /* corpo não-JSON */
    }
    throw new Error(message)
  }
  return res.status === 204 ? (undefined as T) : ((await res.json()) as T)
}

export const api = {
  login: (userName: string, password: string) =>
    request<LoginResponse>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ userName, password }),
    }),

  listTickets: () => request<TicketDto[]>('/api/tickets'),

  getTicket: (id: string) => request<TicketDto>(`/api/tickets/${id}`),

  createTicket: (subject: string, firstMessage?: string) =>
    request<TicketDto>('/api/tickets', {
      method: 'POST',
      body: JSON.stringify({ subject, firstMessage }),
    }),

  updateStatus: (id: string, status: string) =>
    request<TicketDto>(`/api/tickets/${id}/status`, {
      method: 'PATCH',
      body: JSON.stringify({ status }),
    }),

  getMessages: (ticketId: string, before?: string, take = 30) => {
    const qs = new URLSearchParams()
    if (before) qs.set('before', before)
    qs.set('take', String(take))
    return request<MessagePageResponse>(`/api/tickets/${ticketId}/messages?${qs}`)
  },
}
