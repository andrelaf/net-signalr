using SignalRDemo.Domain.Enums;

namespace SignalRDemo.Domain.Entities;

public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserName { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public UserRole Role { get; set; }

    /// <summary>Hash da senha (demo: PBKDF2). Nunca persistir senha em texto puro.</summary>
    public string PasswordHash { get; set; } = default!;

    // Relacionamentos
    public ICollection<Ticket> TicketsOpened { get; set; } = new List<Ticket>();
    public ICollection<Ticket> TicketsAssigned { get; set; } = new List<Ticket>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
