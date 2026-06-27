using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SignalRDemo.Domain.Entities;

namespace SignalRDemo.Api.Auth;

public class JwtTokenService(IOptions<JwtSettings> options)
{
    private readonly JwtSettings _s = options.Value;

    public (string Token, DateTimeOffset ExpiresAt) Create(AppUser user)
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(_s.ExpiryMinutes);

        var claims = new List<Claim>
        {
            // sub / NameIdentifier é o que o CustomUserIdProvider e Clients.User(...) usam.
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new("displayName", user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_s.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _s.Issuer,
            audience: _s.Audience,
            claims: claims,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
