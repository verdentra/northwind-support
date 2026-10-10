using System.Globalization;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Presentation.Authentication;

/// <summary>Reads the signed-in agent from the validated access token of the current request.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public int? AgentId =>
        int.TryParse(Claim(AgentClaims.AgentId), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id
            : null;

    public string? FullName => AgentId is null ? null : Claim(AgentClaims.FullName);

    private string? Claim(string type)
    {
        var user = accessor.HttpContext?.User;

        return user?.Identity?.IsAuthenticated == true ? user.FindFirst(type)?.Value : null;
    }
}
