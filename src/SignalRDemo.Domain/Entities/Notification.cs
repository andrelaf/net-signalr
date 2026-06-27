namespace SignalRDemo.Domain.Entities;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RecipientUserId { get; set; }
    public AppUser Recipient { get; set; } = default!;

    public string Type { get; set; } = default!;
    public string Payload { get; set; } = default!;
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
