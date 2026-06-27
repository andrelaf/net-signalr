using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SignalRDemo.Api.Auth;
using SignalRDemo.Api.Contracts;
using SignalRDemo.Infrastructure.Data;
using SignalRDemo.Infrastructure.Security;

namespace SignalRDemo.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, JwtTokenService tokens) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == request.UserName);
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { error = "Usuário ou senha inválidos." });

        var (token, expiresAt) = tokens.Create(user);
        return new LoginResponse(token, expiresAt, user.Id, user.UserName, user.DisplayName, user.Role.ToString());
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<object> Me() => Ok(new
    {
        Id = User.FindFirstValue(ClaimTypes.NameIdentifier),
        UserName = User.Identity?.Name,
        DisplayName = User.FindFirst("displayName")?.Value,
        Role = User.FindFirstValue(ClaimTypes.Role)
    });
}
