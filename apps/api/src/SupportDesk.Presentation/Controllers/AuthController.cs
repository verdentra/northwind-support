
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    SupportDbContext db,
    IConfiguration configuration) : ControllerBase
{
    public sealed record LoginRequest(string Email, string Password);

    public sealed record AgentUser(
        int Id,
        string FullName,
        string Email);

    public sealed record LoginResponse(
        string Token,
        DateTime ExpiresAtUtc,
        AgentUser User);

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        var email = request.Email.Trim();

        var agent = await db.Set<Agent>()
            .FirstOrDefaultAsync(a => a.Email == email, ct);

        var hasher = new PasswordHasher<Agent>();

        if (agent is null ||
            !agent.IsActive ||
            string.IsNullOrWhiteSpace(agent.PasswordHash))
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        var verification = hasher.VerifyHashedPassword(
            agent,
            agent.PasswordHash,
            request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "JWT signing key is not configured.");

        var issuer = configuration["Jwt:Issuer"]
            ?? "NorthwindSupportDesk";

        var audience = configuration["Jwt:Audience"]
            ?? "NorthwindSupportDesk";

        var lifetimeMinutes = configuration.GetValue(
            "Jwt:LifetimeMinutes", 60);

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(lifetimeMinutes);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, agent.Id.ToString()),
            new Claim(ClaimTypes.Name, agent.FullName),
            new Claim(ClaimTypes.Email, agent.Email)
        };

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(key));

        var credentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var token = new JwtSecurityTokenHandler()
            .WriteToken(jwt);

        return Ok(new LoginResponse(
            token,
            expiresAtUtc,
            new AgentUser(agent.Id, agent.FullName, agent.Email)));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AgentUser>> Me(
        CancellationToken ct)
    {
        var idValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!int.TryParse(idValue, out var agentId))
        {
            return Unauthorized();
        }

        var agent = await db.Set<Agent>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.Id == agentId && a.IsActive, ct);

        if (agent is null)
        {
            return Unauthorized();
        }

        return Ok(new AgentUser(
            agent.Id,
            agent.FullName,
            agent.Email));
    }
}
