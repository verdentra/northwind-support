using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Commands.Login;

/// <summary>
/// Signs an agent in with email and password and issues their access token.
/// </summary>
/// <remarks>
/// Every failure - unknown email, wrong password, no credentials, inactive agent - gives the same
/// 401 and message, and an unknown email still costs a full hash verification, so the response
/// does not reveal which accounts exist.
/// </remarks>
public sealed class LoginCommandHandler(
    IAgentRepository agents,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer,
    ILogger<LoginCommandHandler> logger)
{
    public const string InvalidCredentialsMessage = "Email or password is incorrect.";

    /// <exception cref="UnauthorizedException">The credentials are not accepted.</exception>
    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var agent = await agents.GetByEmailAsync(request.Email.Trim(), ct);

        // Verify first, whatever was found, so every path does the same amount of work.
        var passwordMatches = passwordHasher.Verify(agent?.PasswordHash, request.Password);

        if (agent is null || !passwordMatches || !agent.IsActive)
        {
            logger.LogInformation("Rejected sign-in for {Email}.", request.Email);
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        var current = new CurrentAgentDto(agent.Id, agent.FullName, agent.Email);
        var token = tokenIssuer.Issue(current);

        logger.LogInformation("Agent {AgentId} signed in.", agent.Id);

        return new LoginResponse(token.AccessToken, token.ExpiresAtUtc, current);
    }
}
