using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Application.Abstractions;

public interface IJwtTokenIssuer
{
    IssuedJwt Issue(Agent agent);
}

public sealed record IssuedJwt(string AccessToken, DateTime ExpiresAtUtc);
