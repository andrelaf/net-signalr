using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SignalRDemo.Api.Contracts;
using SignalRDemo.Api.Hubs;
using SignalRDemo.Domain.Entities;
using SignalRDemo.Domain.Enums;
using SignalRDemo.Infrastructure.Data;

namespace SignalRDemo.Api.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize]
public class TicketsController(
    AppDbContext db,
    IHubContext<WorkspaceHub, IWorkspaceClient> hub) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsStaff => User.IsInRole("Agent") || User.IsInRole("Manager");

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TicketDto>>> List()
    {
        var query = db.Tickets
            .Include(t => t.Customer)
            .Include(t => t.AssignedAgent)
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .AsQueryable();

        // Cliente vê apenas os próprios tickets; staff vê todos.
        if (!IsStaff)
        {
            var me = CurrentUserId;
            query = query.Where(t => t.CustomerId == me);
        }

        var items = await query.ToListAsync();
        return items.Select(TicketDto.From).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketDto>> Get(Guid id)
    {
        var ticket = await db.Tickets
            .Include(t => t.Customer)
            .Include(t => t.AssignedAgent)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket is null) return NotFound();
        if (!IsStaff && ticket.CustomerId != CurrentUserId) return Forbid();

        return TicketDto.From(ticket);
    }

    [HttpPost]
    public async Task<ActionResult<TicketDto>> Create(CreateTicketRequest request)
    {
        var ticket = new Ticket
        {
            Subject = request.Subject,
            CustomerId = CurrentUserId,
            Status = TicketStatus.Open
        };
        db.Tickets.Add(ticket);

        if (!string.IsNullOrWhiteSpace(request.FirstMessage))
        {
            db.Messages.Add(new Message
            {
                TicketId = ticket.Id,
                SenderId = CurrentUserId,
                Content = request.FirstMessage.Trim(),
                Kind = MessageKind.Text
            });
        }

        await db.SaveChangesAsync();

        var created = await db.Tickets
            .Include(t => t.Customer)
            .Include(t => t.AssignedAgent)
            .AsNoTracking()
            .FirstAsync(t => t.Id == ticket.Id);

        // Envio FORA do hub: notifica todos via IHubContext sobre o novo ticket.
        await hub.Clients.All.TicketUpdated(TicketDto.From(created));

        return CreatedAtAction(nameof(Get), new { id = ticket.Id }, TicketDto.From(created));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<TicketDto>> UpdateStatus(Guid id, UpdateTicketStatusRequest request)
    {
        if (!Enum.TryParse<TicketStatus>(request.Status, ignoreCase: true, out var status))
            return BadRequest(new { error = "Status inválido." });

        var ticket = await db.Tickets
            .Include(t => t.Customer)
            .Include(t => t.AssignedAgent)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null) return NotFound();

        ticket.Status = status;
        ticket.ResolvedAt = status == TicketStatus.Resolved ? DateTimeOffset.UtcNow : null;
        await db.SaveChangesAsync();

        var dto = TicketDto.From(ticket);

        // IHubContext novamente: broadcast para a sala do ticket + notificação ao cliente.
        await hub.Clients.Group(WorkspaceHub.TicketGroup(id)).TicketUpdated(dto);
        await hub.Clients.User(ticket.CustomerId.ToString()).ReceiveNotification(
            new NotificationDto(Guid.NewGuid(), "TicketStatus",
                $"Seu ticket \"{ticket.Subject}\" agora está {status}.", DateTimeOffset.UtcNow));

        return dto;
    }
}
