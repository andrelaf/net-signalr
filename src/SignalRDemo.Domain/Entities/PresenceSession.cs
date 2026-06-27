namespace SignalRDemo.Domain.Entities;

/// <summary>
/// Auditoria do ciclo de vida de conexões SignalR (OnConnected/OnDisconnected).
/// Útil para mostrar histórico de presença e diagnosticar reconexões.
/// </summary>
public class PresenceSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ConnectionId { get; set; } = default!;
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = default!;
    public DateTimeOffset ConnectedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DisconnectedAt { get; set; }
}
