using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SignalRDemo.Api.Contracts;
using SignalRDemo.Domain.Enums;
using SignalRDemo.Infrastructure.Data;

namespace SignalRDemo.Api.Hubs;

/// <summary>
/// Hub de dashboard ao vivo, restrito a managers. Demonstra streaming SERVIDOR -> CLIENTE
/// via <see cref="IAsyncEnumerable{T}"/>: o cliente assina <c>StreamMetrics</c> e recebe
/// KPIs continuamente até cancelar (unsubscribe) — o CancellationToken é acionado nesse momento.
/// </summary>
[Authorize(Policy = "ManagersOnly")]
public class DashboardHub(IServiceScopeFactory scopeFactory, PresenceTracker presence) : Hub
{
    public async IAsyncEnumerable<DashboardMetricsDto> StreamMetrics(
        int intervalSeconds,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Clamp defensivo do intervalo para evitar flood.
        var delay = TimeSpan.FromSeconds(Math.Clamp(intervalSeconds, 1, 60));

        while (!cancellationToken.IsCancellationRequested)
        {
            // Escopo novo por tick: evita manter um DbContext vivo por toda a duração do stream.
            DashboardMetricsDto metrics;
            using (var scope = scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var since = DateTimeOffset.UtcNow.AddHours(-1);

                metrics = new DashboardMetricsDto(
                    Timestamp: DateTimeOffset.UtcNow,
                    OpenTickets: await db.Tickets.CountAsync(t => t.Status == TicketStatus.Open, cancellationToken),
                    PendingTickets: await db.Tickets.CountAsync(t => t.Status == TicketStatus.Pending, cancellationToken),
                    ResolvedTickets: await db.Tickets.CountAsync(t => t.Status == TicketStatus.Resolved, cancellationToken),
                    OnlineUsers: presence.OnlineUsers().Count,
                    MessagesLastHour: await db.Messages.CountAsync(m => m.SentAt >= since, cancellationToken));
            }

            yield return metrics;

            try
            {
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
        }
    }
}
