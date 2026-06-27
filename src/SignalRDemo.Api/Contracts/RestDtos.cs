namespace SignalRDemo.Api.Contracts;

public record CreateTicketRequest(string Subject, string? FirstMessage);

public record UpdateTicketStatusRequest(string Status);

public record MessagePageResponse(IReadOnlyList<MessageDto> Items, bool HasMore);
