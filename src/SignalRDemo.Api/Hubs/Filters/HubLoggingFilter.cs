using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;

namespace SignalRDemo.Api.Hubs.Filters;

/// <summary>
/// Hub filter (cross-cutting) que mede e loga cada invocação de método de hub,
/// além do ciclo de conexão. Registrado globalmente em AddSignalR(o => o.AddFilter...).
/// </summary>
public class HubLoggingFilter(ILogger<HubLoggingFilter> logger) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext context,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var sw = Stopwatch.StartNew();
        var method = context.HubMethodName;
        var user = context.Context.User?.Identity?.Name ?? "anon";
        try
        {
            var result = await next(context);
            logger.LogInformation("Hub {Method} por {User} em {Elapsed}ms", method, user, sw.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Hub {Method} por {User} FALHOU em {Elapsed}ms", method, user, sw.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        await next(context);
    }

    public Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
        => next(context, exception);
}
