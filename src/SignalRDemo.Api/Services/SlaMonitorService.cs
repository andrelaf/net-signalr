using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SignalRDemo.Api.Contracts;
using SignalRDemo.Api.Hubs;
using SignalRDemo.Domain.Entities;
using SignalRDemo.Domain.Enums;
using SignalRDemo.Infrastructure.Data;

namespace SignalRDemo.Api.Services;

/// <summary>
/// BackgroundService que monitora SLA: tickets ainda "Open" há mais que o limite configurado
/// geram um alerta enviado por push (IHubContext) — demonstra envio de mensagens SignalR
/// de FORA de um hub, a partir de um serviço hospedado. Cada ticket alerta apenas uma vez.
/// </summary>
public class SlaMonitorService(
    IServiceScopeFactory scopeFactory,
    IHubContext<WorkspaceHub, IWorkspaceClient> hub,
    IConfiguration config,
    ILogger<SlaMonitorService> logger) : BackgroundService
{
    private readonly int _breachHours = config.GetValue("Sla:BreachAfterHours", 24);
    private readonly int _pollSeconds = config.GetValue("Sla:PollSeconds", 30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Pequeno atraso inicial para a aplicação subir/seed concluir.
        try { await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha no monitor de SLA");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(_pollSeconds), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task CheckOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var threshold = DateTimeOffset.UtcNow.AddHours(-_breachHours);

        var breached = await db.Tickets
            .Where(t => t.Status == TicketStatus.Open && t.CreatedAt < threshold)
            .ToListAsync(ct);

        foreach (var ticket in breached)
        {
            // Já alertado? (idempotência via Notification persistida)
            var marker = $"sla:{ticket.Id}";
            var already = await db.Notifications.AnyAsync(
                n => n.Type == "SlaBreach" && n.Payload.StartsWith(marker), ct);
            if (already) continue;

            // Destinatário: agente responsável; se não houver, todos os managers.
            var recipients = ticket.AssignedAgentId is { } agentId
                ? new[] { agentId }
                : await db.Users.Where(u => u.Role == UserRole.Manager).Select(u => u.Id).ToArrayAsync(ct);

            var payload = $"{marker}|SLA estourado: ticket \"{ticket.Subject}\" aberto há mais de {_breachHours}h.";

            foreach (var recipientId in recipients)
            {
                db.Notifications.Add(new Notification
                {
                    RecipientUserId = recipientId,
                    Type = "SlaBreach",
                    Payload = payload
                });
            }
            await db.SaveChangesAsync(ct);

            foreach (var recipientId in recipients)
            {
                await hub.Clients.User(recipientId.ToString()).ReceiveNotification(
                    new NotificationDto(Guid.NewGuid(), "SlaBreach", payload, DateTimeOffset.UtcNow));
            }

            logger.LogWarning("Alerta de SLA enviado para ticket {Ticket}", ticket.Id);
        }
    }
}
