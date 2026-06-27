using Microsoft.EntityFrameworkCore;
using SignalRDemo.Domain.Entities;
using SignalRDemo.Domain.Enums;
using SignalRDemo.Infrastructure.Security;

namespace SignalRDemo.Infrastructure.Data;

/// <summary>
/// Aplica migrations e popula dados de demonstração. Todos os usuários têm senha "demo123".
/// </summary>
public static class DbInitializer
{
    public const string DemoPassword = "demo123";

    public static async Task InitializeAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync();

        if (await db.Users.AnyAsync())
            return;

        string Hash() => PasswordHasher.Hash(DemoPassword);

        var manager = new AppUser { UserName = "manager", DisplayName = "Marina (Manager)", Role = UserRole.Manager, PasswordHash = Hash() };
        var agentAna = new AppUser { UserName = "ana", DisplayName = "Ana (Agente)", Role = UserRole.Agent, PasswordHash = Hash() };
        var agentBruno = new AppUser { UserName = "bruno", DisplayName = "Bruno (Agente)", Role = UserRole.Agent, PasswordHash = Hash() };
        var custCarla = new AppUser { UserName = "carla", DisplayName = "Carla (Cliente)", Role = UserRole.Customer, PasswordHash = Hash() };
        var custDiego = new AppUser { UserName = "diego", DisplayName = "Diego (Cliente)", Role = UserRole.Customer, PasswordHash = Hash() };

        db.Users.AddRange(manager, agentAna, agentBruno, custCarla, custDiego);

        var t1 = new Ticket
        {
            Subject = "Não consigo fazer login no portal",
            Status = TicketStatus.Open,
            CustomerId = custCarla.Id,
            AssignedAgentId = agentAna.Id,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-3)
        };
        var t2 = new Ticket
        {
            Subject = "Cobrança duplicada na fatura",
            Status = TicketStatus.Pending,
            CustomerId = custDiego.Id,
            AssignedAgentId = agentBruno.Id,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-26) // antigo -> dispara alerta de SLA
        };
        var t3 = new Ticket
        {
            Subject = "Dúvida sobre plano premium",
            Status = TicketStatus.Open,
            CustomerId = custCarla.Id,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-20)
        };
        db.Tickets.AddRange(t1, t2, t3);

        db.Messages.AddRange(
            new Message { TicketId = t1.Id, SenderId = custCarla.Id, Content = "Olá, recebo 'senha inválida' mesmo com a senha certa.", SentAt = t1.CreatedAt },
            new Message { TicketId = t1.Id, SenderId = agentAna.Id, Content = "Oi Carla! Vou verificar sua conta, um momento.", SentAt = t1.CreatedAt.AddMinutes(2) },
            new Message { TicketId = t2.Id, SenderId = custDiego.Id, Content = "Fui cobrado duas vezes este mês.", SentAt = t2.CreatedAt }
        );

        await db.SaveChangesAsync();
    }
}
