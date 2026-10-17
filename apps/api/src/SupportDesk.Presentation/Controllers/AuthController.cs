using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(
    LoginCommandHandler login,
    ICurrentUser currentUser,
    IAgentRepository agents) : ControllerBase
{
    private const string InvalidCredentialsMessage = "Invalid email or password.";

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginRequest request, CancellationToken ct)
    {
        var response = await login.HandleAsync(request, ct);

        if (response is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = InvalidCredentialsMessage,
                Status = StatusCodes.Status401Unauthorized
            });
        }

        return Ok(response);
    }

    [HttpGet("me")]
    public async Task<ActionResult<AgentIdentityDto>> Me(CancellationToken ct)
    {
        if (currentUser.AgentId is not { } agentId || string.IsNullOrWhiteSpace(currentUser.Email))
        {
            return Unauthorized();
        }

        var agent = await agents.GetByIdAsync(agentId, ct);

        if (agent is null || !string.Equals(agent.Email, currentUser.Email, StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized();
        }

        return Ok(new AgentIdentityDto(agent.Id, agent.FullName, agent.Email));
    }
}
