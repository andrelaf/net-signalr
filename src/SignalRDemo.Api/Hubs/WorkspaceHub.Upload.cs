using Microsoft.AspNetCore.SignalR;
using SignalRDemo.Api.Contracts;
using SignalRDemo.Domain.Entities;
using SignalRDemo.Domain.Enums;

namespace SignalRDemo.Api.Hubs;

/// <summary>
/// Parte do WorkspaceHub que demonstra streaming CLIENTE -> SERVIDOR.
/// O cliente envia os chunks do arquivo como <see cref="IAsyncEnumerable{T}"/>; o servidor
/// grava em disco, reporta progresso de volta (server->client) e, ao concluir, persiste o
/// anexo + uma mensagem do tipo File e faz broadcast para o grupo do ticket.
/// </summary>
public partial class WorkspaceHub
{
    public async Task<Guid> UploadAttachment(UploadMetadata meta, IAsyncEnumerable<byte[]> chunks)
    {
        await EnsureCanAccessTicket(meta.TicketId);

        if (meta.TotalSize > 25 * 1024 * 1024)
            throw new HubException("Arquivo excede o limite de 25 MB.");

        var uploadsDir = Path.Combine(AppContext.BaseDirectory, "uploads");
        Directory.CreateDirectory(uploadsDir);

        var safeName = Path.GetFileName(meta.FileName);
        var storedName = $"{Guid.NewGuid():N}_{safeName}";
        var fullPath = Path.Combine(uploadsDir, storedName);

        long received = 0;
        var lastPercent = -1;

        await using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write))
        {
            await foreach (var chunk in chunks.WithCancellation(Context.ConnectionAborted))
            {
                await fs.WriteAsync(chunk);
                received += chunk.Length;

                var percent = meta.TotalSize > 0 ? (int)(received * 100 / meta.TotalSize) : 100;
                if (percent != lastPercent)
                {
                    lastPercent = percent;
                    await Clients.Caller.UploadProgress(meta.UploadId, percent);
                }
            }
        }

        // Persiste a mensagem (tipo File) + o anexo.
        var message = new Message
        {
            TicketId = meta.TicketId,
            SenderId = CurrentUserId,
            Content = safeName,
            Kind = MessageKind.File,
            Attachment = new Attachment
            {
                FileName = safeName,
                ContentType = meta.ContentType,
                SizeBytes = received,
                StoragePath = storedName
            }
        };
        db.Messages.Add(message);
        await db.SaveChangesAsync();

        var dto = new MessageDto(message.Id, meta.TicketId, CurrentUserId, CurrentDisplayName,
            message.Content, message.Kind.ToString(), message.SentAt,
            AttachmentDto.From(message.Attachment));

        await Clients.Group(TicketGroup(meta.TicketId)).ReceiveMessage(dto);
        await Clients.Caller.UploadProgress(meta.UploadId, 100);

        return message.Attachment.Id;
    }
}
