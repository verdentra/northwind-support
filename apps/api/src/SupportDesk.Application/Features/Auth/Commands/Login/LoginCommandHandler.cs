using Microsoft.AspNetCore.Identity;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Commands.Login;

public sealed class LoginCommandHandler(
    IAgentRepository agents,
    IPasswordHasher<Agent> passwordHasher,
    IJwtTokenIssuer tokenIssuer)
{
    public async Task<LoginResponseDto?> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();
        var agent = await agents.GetByEmailAsync(email, ct);

        if (agent is null || !agent.IsActive || string.IsNullOrWhiteSpace(agent.PasswordHash))
        {
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(
            agent,
            agent.PasswordHash,
            request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        var token = tokenIssuer.Issue(agent);

        return new LoginResponseDto(
            token.AccessToken,
            token.ExpiresAtUtc,
            new AgentIdentityDto(agent.Id, agent.FullName, agent.Email));
    }
}
