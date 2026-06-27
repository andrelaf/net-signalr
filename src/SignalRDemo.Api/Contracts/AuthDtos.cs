namespace SignalRDemo.Api.Contracts;

public record LoginRequest(string UserName, string Password);

public record LoginResponse(
    string Token,
    DateTimeOffset ExpiresAt,
    Guid UserId,
    string UserName,
    string DisplayName,
    string Role);
