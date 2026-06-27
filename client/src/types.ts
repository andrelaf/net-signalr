// Tipos espelhando os DTOs do backend (SignalRDemo.Api.Contracts).

export interface LoginResponse {
  token: string
  expiresAt: string
  userId: string
  userName: string
  displayName: string
  role: 'Customer' | 'Agent' | 'Manager'
}

export interface AttachmentDto {
  id: string
  fileName: string
  contentType: string
  sizeBytes: number
}

export interface MessageDto {
  id: string
  ticketId: string
  senderId: string
  senderName: string
  content: string
  kind: 'Text' | 'System' | 'File'
  sentAt: string
  attachment: AttachmentDto | null
}

export interface TicketDto {
  id: string
  subject: string
  status: 'Open' | 'Pending' | 'Resolved'
  customerId: string
  customerName: string
  assignedAgentId: string | null
  assignedAgentName: string | null
  createdAt: string
  rowVersion: string
}

export interface PresenceDto {
  userId: string
  displayName: string
  online: boolean
}

export interface TypingDto {
  ticketId: string
  userId: string
  displayName: string
  isTyping: boolean
}

export interface NotificationDto {
  id: string
  type: string
  payload: string
  createdAt: string
}

export interface DirectMessageDto {
  fromUserId: string
  fromDisplayName: string
  content: string
  sentAt: string
}

export interface DashboardMetricsDto {
  timestamp: string
  openTickets: number
  pendingTickets: number
  resolvedTickets: number
  onlineUsers: number
  messagesLastHour: number
}

export interface MessagePageResponse {
  items: MessageDto[]
  hasMore: boolean
}
