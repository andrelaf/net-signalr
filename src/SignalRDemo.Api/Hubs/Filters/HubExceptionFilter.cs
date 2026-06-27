using Microsoft.AspNetCore.SignalR;

namespace SignalRDemo.Api.Hubs.Filters;

/// <summary>
/// Hub filter que padroniza erros: HubException (mensagens "de negócio") passam direto,
/// mas exceções inesperadas são logadas e convertidas em uma mensagem genérica, evitando
/// vazar detalhes internos ao cliente quando EnableDetailedErrors está desligado.
/// </summary>
public class HubExceptionFilter(ILogger<HubExceptionFilter> logger) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext context,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(context);
        }
        catch (HubException)
        {
            throw; // erro de negócio esperado -> repassa a mensagem ao cliente
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro não tratado em {Method}", context.HubMethodName);
            throw new HubException("Ocorreu um erro ao processar sua solicitação.");
        }
    }
}
