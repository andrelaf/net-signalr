using SignalRDemo.Api.Contracts;

namespace SignalRDemo.Api.Hubs;

/// <summary>
/// Métodos que o SERVIDOR chama no CLIENTE (hub fortemente tipado).
/// O método <see cref="ConfirmAction"/> retorna <c>Task&lt;bool&gt;</c> e demonstra
/// "client results": o servidor invoca o cliente e aguarda uma resposta.
/// </summary>
public interface IWorkspaceClient
{
    Task ReceiveMessage(MessageDto message);
    Task UserJoined(Guid ticketId, Guid userId, string displayName);
    Task UserLeft(Guid ticketId, Guid userId, string displayName);
    Task TypingChanged(TypingDto typing);
    Task PresenceChanged(PresenceDto presence);
    Task TicketUpdated(TicketDto ticket);
    Task ReceiveNotification(NotificationDto notification);
    Task ReceiveDirectMessage(DirectMessageDto dm);
    Task UploadProgress(Guid uploadId, int percent);

    /// <summary>Client result: pede confirmação ao cliente e espera um booleano de volta.</summary>
    Task<bool> ConfirmAction(string prompt);
}
