using SignalRDemo.Domain.Entities;

namespace SignalRDemo.Api.Contracts;

public record MessageDto(
    Guid Id,
    Guid TicketId,
    Guid SenderId,
    string SenderName,
    string Content,
    string Kind,
    DateTimeOffset SentAt,
    AttachmentDto? Attachment)
{
    public static MessageDto From(Message m) => new(
        m.Id, m.TicketId, m.SenderId,
        m.Sender?.DisplayName ?? "?",
        m.Content, m.Kind.ToString(), m.SentAt,
        m.Attachment is null ? null : AttachmentDto.From(m.Attachment));
}

public record AttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes)
{
    public static AttachmentDto From(Attachment a) => new(a.Id, a.FileName, a.ContentType, a.SizeBytes);
}

public record PresenceDto(Guid UserId, string DisplayName, bool Online);

public record TypingDto(Guid TicketId, Guid UserId, string DisplayName, bool IsTyping);

public record TicketDto(
    Guid Id,
    string Subject,
    string Status,
    Guid CustomerId,
    string CustomerName,
    Guid? AssignedAgentId,
    string? AssignedAgentName,
    DateTimeOffset CreatedAt,
    Guid RowVersion)
{
    public static TicketDto From(Ticket t) => new(
        t.Id, t.Subject, t.Status.ToString(),
        t.CustomerId, t.Customer?.DisplayName ?? "?",
        t.AssignedAgentId, t.AssignedAgent?.DisplayName,
        t.CreatedAt, t.RowVersion);
}

public record NotificationDto(Guid Id, string Type, string Payload, DateTimeOffset CreatedAt);

public record DirectMessageDto(Guid FromUserId, string FromDisplayName, string Content, DateTimeOffset SentAt);
