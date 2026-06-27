using SignalRDemo.Domain.Enums;

namespace SignalRDemo.Domain.Entities;

public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TicketId { get; set; }
    public Ticket Ticket { get; set; } = default!;

    public Guid SenderId { get; set; }
    public AppUser Sender { get; set; } = default!;

    public string Content { get; set; } = default!;
    public MessageKind Kind { get; set; } = MessageKind.Text;
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;

    public Attachment? Attachment { get; set; }
}
