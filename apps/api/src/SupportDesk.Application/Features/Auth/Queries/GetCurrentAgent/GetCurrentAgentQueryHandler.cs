using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Queries.GetCurrentAgent;

/// <summary>
/// Returns the signed-in agent. Re-reads the agent, so one deactivated after signing in is
/// refused here even though their token has not expired yet.
/// </summary>
public sealed class GetCurrentAgentQueryHandler(ICurrentUser currentUser, IAgentRepository agents)
{
    /// <exception cref="UnauthorizedException">No signed-in agent, or they no longer exist or are inactive.</exception>
    public async Task<CurrentAgentDto> HandleAsync(CancellationToken ct)
    {
        if (currentUser.AgentId is not { } agentId)
        {
            throw new UnauthorizedException("You are not signed in.");
        }

        var agent = await agents.GetByIdAsync(agentId, ct);

        if (agent is null || !agent.IsActive)
        {
            throw new UnauthorizedException("Your account is no longer active. Sign in again.");
        }

        return new CurrentAgentDto(agent.Id, agent.FullName, agent.Email);
    }
}
