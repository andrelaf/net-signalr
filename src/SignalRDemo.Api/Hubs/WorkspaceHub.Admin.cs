using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SignalRDemo.Api.Contracts;
using SignalRDemo.Domain.Enums;

namespace SignalRDemo.Api.Hubs;

/// <summary>
/// Parte do WorkspaceHub com comandos administrativos. Demonstra:
///  - Autorização por método ([Authorize(Policy = "StaffOnly")]);
///  - Concorrência otimista do EF Core (RowVersion) ao mudar status do ticket;
///  - Client results: o servidor invoca o cliente e AGUARDA uma resposta booleana.
/// </summary>
public partial class WorkspaceHub
{
    [Authorize(Policy = "StaffOnly")]
    public async Task AssignTicket(Guid ticketId, Guid agentId)
    {
        var ticket = await db.Tickets
            .Include(t => t.Customer)
            .Include(t => t.AssignedAgent)
            .FirstOrDefaultAsync(t => t.Id == ticketId)
            ?? throw new HubException("Ticket não encontrado.");

        ticket.AssignedAgentId = agentId;
        await db.SaveChangesAsync();
        await BroadcastTicketUpdated(ticketId);
    }

    /// <summary>
    /// Resolve o ticket aplicando concorrência otimista: o cliente informa a RowVersion que
    /// enxergou; se outro agente alterou o ticket nesse meio tempo, lança conflito.
    /// </summary>
    [Authorize(Policy = "StaffOnly")]
    public async Task ResolveTicket(Guid ticketId, Guid expectedRowVersion)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId)
            ?? throw new HubException("Ticket não encontrado.");

        // Usa a versão que o cliente viu como valor "original" para a checagem de concorrência.
        db.Entry(ticket).Property(t => t.RowVersion).OriginalValue = expectedRowVersion;

        ticket.Status = TicketStatus.Resolved;
        ticket.ResolvedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new HubException("O ticket foi alterado por outra pessoa. Recarregue e tente novamente.");
        }

        await BroadcastTicketUpdated(ticketId);
    }

    /// <summary>
    /// Client result: pede ao cliente (cliente-dono do ticket) que confirme o encerramento.
    /// O servidor invoca ConfirmAction em uma conexão específica e aguarda o retorno booleano.
    /// </summary>
    [Authorize(Policy = "StaffOnly")]
    public async Task<bool> RequestCloseConfirmation(Guid ticketId)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId)
            ?? throw new HubException("Ticket não encontrado.");

        var connections = presence.ConnectionsOf(ticket.CustomerId);
        if (connections.Count == 0)
            throw new HubException("O cliente não está online para confirmar.");

        // Client results exigem um único cliente: usamos Clients.Client(connectionId).
        bool confirmed;
        try
        {
            confirmed = await Clients.Client(connections.First())
                .ConfirmAction($"O atendente deseja encerrar o ticket \"{ticket.Subject}\". Você confirma?");
        }
        catch (Exception ex)
        {
            throw new HubException($"Não foi possível obter confirmação do cliente: {ex.Message}");
        }

        if (confirmed)
        {
            ticket.Status = TicketStatus.Resolved;
            ticket.ResolvedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            await BroadcastTicketUpdated(ticketId);
        }

        return confirmed;
    }

    private async Task BroadcastTicketUpdated(Guid ticketId)
    {
        var ticket = await db.Tickets
            .Include(t => t.Customer)
            .Include(t => t.AssignedAgent)
            .AsNoTracking()
            .FirstAsync(t => t.Id == ticketId);

        await Clients.Group(TicketGroup(ticketId)).TicketUpdated(TicketDto.From(ticket));
    }
}
