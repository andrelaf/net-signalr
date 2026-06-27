using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace SignalRDemo.Api.Auth;

/// <summary>
/// Define qual claim o SignalR usa como "user id" em Clients.User(id) e IHubContext.Clients.User(id).
/// Por padrão o SignalR usa ClaimTypes.NameIdentifier; aqui deixamos explícito para clareza
/// e para garantir alinhamento com o "sub" do JWT.
/// </summary>
public class CustomUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
