using SignalRDemo.Domain.Enums;

namespace SignalRDemo.Domain.Entities;

public class Ticket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Subject { get; set; } = default!;
    public TicketStatus Status { get; set; } = TicketStatus.Open;

    public Guid CustomerId { get; set; }
    public AppUser Customer { get; set; } = default!;

    public Guid? AssignedAgentId { get; set; }
    public AppUser? AssignedAgent { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>
    /// Token de concorrência otimista. Garante que dois agentes não sobrescrevam
    /// o status do mesmo ticket simultaneamente — combina EF Core + push via SignalR.
    /// </summary>
    public Guid RowVersion { get; set; } = Guid.NewGuid();

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
