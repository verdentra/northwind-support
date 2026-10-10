using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;

namespace SupportDesk.Presentation.Authentication;

/// <summary>Issues HS256-signed JWTs carrying the agent's id, name and email.</summary>
public sealed class JwtTokenIssuer(IOptions<JwtOptions> options, IClock clock) : ITokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public IssuedToken Issue(CurrentAgentDto agent)
    {
        var settings = options.Value;
        var now = clock.UtcNow;
        var expires = now.AddMinutes(settings.TokenLifetimeMinutes);

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(AgentClaims.AgentId, agent.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(AgentClaims.FullName, agent.FullName),
                new Claim(AgentClaims.Email, agent.Email),
                new Claim(AgentClaims.TokenId, Guid.NewGuid().ToString("N"))
            }),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(settings.SigningKeyBytes()), SecurityAlgorithms.HmacSha256)
        });

        return new IssuedToken(token, expires);
    }
}
