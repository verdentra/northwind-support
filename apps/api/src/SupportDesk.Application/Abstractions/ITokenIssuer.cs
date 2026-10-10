using SupportDesk.Application.Contracts.Auth;

namespace SupportDesk.Application.Abstractions;

/// <summary>Issues the access token a signed-in agent sends with every request.</summary>
public interface ITokenIssuer
{
    IssuedToken Issue(CurrentAgentDto agent);
}

/// <param name="AccessToken">The signed token.</param>
/// <param name="ExpiresAtUtc">When it stops being accepted.</param>
public sealed record IssuedToken(string AccessToken, DateTime ExpiresAtUtc);
