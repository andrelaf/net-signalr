using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SignalRDemo.Api.Contracts;
using SignalRDemo.Infrastructure.Data;

namespace SignalRDemo.Api.Controllers;

[ApiController]
[Route("api/tickets/{ticketId:guid}/messages")]
[Authorize]
public class MessagesController(AppDbContext db) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsStaff => User.IsInRole("Agent") || User.IsInRole("Manager");

    /// <summary>
    /// Histórico paginado (keyset por SentAt) para o cliente carregar ao abrir a sala,
    /// antes de receber novas mensagens em tempo real pelo hub.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<MessagePageResponse>> Get(
        Guid ticketId,
        [FromQuery] DateTimeOffset? before,
        [FromQuery] int take = 30)
    {
        take = Math.Clamp(take, 1, 100);

        var ticket = await db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId);
        if (ticket is null) return NotFound();
        if (!IsStaff && ticket.CustomerId != CurrentUserId) return Forbid();

        var query = db.Messages
            .Include(m => m.Sender)
            .Include(m => m.Attachment)
            .AsNoTracking()
            .Where(m => m.TicketId == ticketId);

        if (before is not null)
            query = query.Where(m => m.SentAt < before);

        // Pega take+1 para saber se há mais páginas; ordena desc e devolve em ordem cronológica.
        var page = await query
            .OrderByDescending(m => m.SentAt)
            .Take(take + 1)
            .ToListAsync();

        var hasMore = page.Count > take;
        var items = page.Take(take)
            .OrderBy(m => m.SentAt)
            .Select(MessageDto.From)
            .ToList();

        return new MessagePageResponse(items, hasMore);
    }
}
