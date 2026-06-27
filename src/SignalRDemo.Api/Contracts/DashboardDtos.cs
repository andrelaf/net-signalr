namespace SignalRDemo.Api.Contracts;

public record DashboardMetricsDto(
    DateTimeOffset Timestamp,
    int OpenTickets,
    int PendingTickets,
    int ResolvedTickets,
    int OnlineUsers,
    int MessagesLastHour);

/// <summary>Metadados enviados pelo cliente antes/junto do stream de chunks do upload.</summary>
public record UploadMetadata(
    Guid UploadId,
    Guid TicketId,
    string FileName,
    string ContentType,
    long TotalSize);
