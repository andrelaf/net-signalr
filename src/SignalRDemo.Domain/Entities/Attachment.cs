namespace SignalRDemo.Domain.Entities;

public class Attachment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MessageId { get; set; }
    public Message Message { get; set; } = default!;

    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long SizeBytes { get; set; }

    /// <summary>Caminho relativo no storage local de demo (pasta uploads/).</summary>
    public string StoragePath { get; set; } = default!;
}
