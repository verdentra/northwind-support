using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.Presentation.Auth;
using SupportDesk.Presentation.Contracts.Auth;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(
    IAgentRepository agents,
    IPasswordHasher<Agent> passwordHasher,
    IOptions<JwtOptions> jwtOptions,
    IClock clock) : ControllerBase
{
    private const string InvalidCredentialsMessage = "Email or password is incorrect.";
    private const string DummyPassword = "This-is-not-a-valid-user-password";
    private static readonly Agent DummyAgent = new(
        "Invalid User",
        "invalid@example.invalid",
        1,
        DateTime.UnixEpoch);
    private static readonly string DummyPasswordHash =
        new PasswordHasher<Agent>().HashPassword(DummyAgent, DummyPassword);

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken ct)
    {
        var agent = await agents.GetByEmailAsync(request.Email.Trim(), ct);
        var hasValidPassword = agent?.PasswordHash is { } hash
            ? passwordHasher.VerifyHashedPassword(agent, hash, request.Password) !=
              PasswordVerificationResult.Failed
            : passwordHasher.VerifyHashedPassword(
                  DummyAgent, DummyPasswordHash, request.Password) !=
              PasswordVerificationResult.Failed;

        if (agent is null || !agent.IsActive || !hasValidPassword)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = InvalidCredentialsMessage,
                Status = StatusCodes.Status401Unauthorized
            });
        }

        var options = jwtOptions.Value;
        var expiresAtUtc = clock.UtcNow.AddMinutes(options.AccessTokenMinutes);
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, agent.Id.ToString()),
            new Claim(ClaimTypes.Name, agent.FullName),
            new Claim(ClaimTypes.Email, agent.Email)
        };
        var token = new JwtSecurityToken(
            options.Issuer,
            options.Audience,
            claims,
            notBefore: clock.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return Ok(new LoginResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAtUtc,
            new AuthenticatedAgent(agent.Id, agent.FullName, agent.Email)));
    }

    [HttpGet("me")]
    public ActionResult<AuthenticatedAgent> Me()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var name = User.FindFirstValue(ClaimTypes.Name);
        var email = User.FindFirstValue(ClaimTypes.Email);

        if (!int.TryParse(idClaim, out var id) ||
            string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(email))
        {
            return Unauthorized();
        }

        return Ok(new AuthenticatedAgent(id, name, email));
    }
}
