using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Presentation.Authentication;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public string? Subject => Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);

    public string? Email => Principal?.FindFirstValue(JwtRegisteredClaimNames.Email);

    public int? AgentId => int.TryParse(Subject, out var id) ? id : null;
}
