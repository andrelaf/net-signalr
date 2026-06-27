namespace SignalRDemo.Api.Auth;

public class JwtSettings
{
    public string Issuer { get; set; } = default!;
    public string Audience { get; set; } = default!;
    public string SigningKey { get; set; } = default!;
    public int ExpiryMinutes { get; set; } = 120;
}
