using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SignalRDemo.Api.Contracts;
using SignalRDemo.Domain.Entities;
using SignalRDemo.Domain.Enums;
using SignalRDemo.Infrastructure.Data;

namespace SignalRDemo.Api.Hubs;

/// <summary>
/// Hub central do workspace: chat por ticket (Groups), mensagens diretas (Clients.User),
/// presença + typing (ciclo de vida das conexões) e — nos arquivos de streaming/admin —
/// upload em streaming, client results e comandos administrativos.
/// </summary>
[Authorize]
public partial class WorkspaceHub(
    AppDbContext db,
    PresenceTracker presence,
    ILogger<WorkspaceHub> logger) : Hub<IWorkspaceClient>
{
    internal static string TicketGroup(Guid ticketId) => $"ticket:{ticketId}";

    private Guid CurrentUserId => Guid.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string CurrentDisplayName => Context.User!.FindFirst("displayName")?.Value ?? "?";
    private bool IsStaff => Context.User!.IsInRole("Agent") || Context.User!.IsInRole("Manager");

    // -----------------------------------------------------------------------
    // Ciclo de vida da conexão -> presença
    // -----------------------------------------------------------------------
    public override async Task OnConnectedAsync()
    {
        var userId = CurrentUserId;
        var firstConnection = presence.Connect(userId, CurrentDisplayName, Context.ConnectionId);

        db.PresenceSessions.Add(new PresenceSession
        {
            ConnectionId = Context.ConnectionId,
            UserId = userId
        });
        await db.SaveChangesAsync();

        // Envia ao recém-conectado a lista de quem já está online.
        foreach (var (id, name) in presence.OnlineUsers())
            await Clients.Caller.PresenceChanged(new PresenceDto(id, name, true));

        if (firstConnection)
            await Clients.Others.PresenceChanged(new PresenceDto(userId, CurrentDisplayName, true));

        logger.LogInformation("Conectado {User} ({Conn})", CurrentDisplayName, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = CurrentUserId;
        var lastConnection = presence.Disconnect(userId, Context.ConnectionId);

        var session = await db.PresenceSessions
            .Where(s => s.ConnectionId == Context.ConnectionId && s.DisconnectedAt == null)
            .OrderByDescending(s => s.ConnectedAt)
            .FirstOrDefaultAsync();
        if (session is not null)
        {
            session.DisconnectedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        if (lastConnection)
            await Clients.Others.PresenceChanged(new PresenceDto(userId, CurrentDisplayName, false));

        await base.OnDisconnectedAsync(exception);
    }

    // -----------------------------------------------------------------------
    // Groups: salas de ticket
    // -----------------------------------------------------------------------
    public async Task JoinTicket(Guid ticketId)
    {
        await EnsureCanAccessTicket(ticketId);

        await Groups.AddToGroupAsync(Context.ConnectionId, TicketGroup(ticketId));
        await Clients.OthersInGroup(TicketGroup(ticketId))
            .UserJoined(ticketId, CurrentUserId, CurrentDisplayName);

        logger.LogInformation("{User} entrou no ticket {Ticket}", CurrentDisplayName, ticketId);
    }

    public async Task LeaveTicket(Guid ticketId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, TicketGroup(ticketId));
        await Clients.OthersInGroup(TicketGroup(ticketId))
            .UserLeft(ticketId, CurrentUserId, CurrentDisplayName);
    }

    // -----------------------------------------------------------------------
    // Mensagens de chat (persistidas) -> broadcast para o grupo do ticket
    // -----------------------------------------------------------------------
    public async Task SendMessage(Guid ticketId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new HubException("Mensagem vazia.");
        if (content.Length > 4000)
            throw new HubException("Mensagem muito longa (máx. 4000).");

        await EnsureCanAccessTicket(ticketId);

        var message = new Message
        {
            TicketId = ticketId,
            SenderId = CurrentUserId,
            Content = content.Trim(),
            Kind = MessageKind.Text
        };
        db.Messages.Add(message);
        await db.SaveChangesAsync();

        var dto = new MessageDto(message.Id, ticketId, CurrentUserId, CurrentDisplayName,
            message.Content, message.Kind.ToString(), message.SentAt, null);

        await Clients.Group(TicketGroup(ticketId)).ReceiveMessage(dto);
    }

    // -----------------------------------------------------------------------
    // Typing indicator -> apenas para os outros no grupo
    // -----------------------------------------------------------------------
    public Task Typing(Guid ticketId, bool isTyping) =>
        Clients.OthersInGroup(TicketGroup(ticketId))
            .TypingChanged(new TypingDto(ticketId, CurrentUserId, CurrentDisplayName, isTyping));

    // -----------------------------------------------------------------------
    // Mensagem direta usuário->usuário (Clients.User usa o IUserIdProvider)
    // -----------------------------------------------------------------------
    public async Task SendDirectMessage(Guid toUserId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new HubException("Mensagem vazia.");

        var dto = new DirectMessageDto(CurrentUserId, CurrentDisplayName, content.Trim(), DateTimeOffset.UtcNow);
        await Clients.User(toUserId.ToString()).ReceiveDirectMessage(dto);
    }

    // -----------------------------------------------------------------------
    // Autorização de acesso ao ticket: dono (cliente) ou staff (agente/manager)
    // -----------------------------------------------------------------------
    private async Task EnsureCanAccessTicket(Guid ticketId)
    {
        if (IsStaff) return;

        var ownsTicket = await db.Tickets
            .AnyAsync(t => t.Id == ticketId && t.CustomerId == CurrentUserId);
        if (!ownsTicket)
            throw new HubException("Você não tem acesso a este ticket.");
    }
}
