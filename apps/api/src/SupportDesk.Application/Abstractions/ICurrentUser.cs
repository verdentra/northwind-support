namespace SupportDesk.Application.Abstractions;

/// <summary>
/// The agent making the current request, as established by their access token. Both values are
/// null when the request is not authenticated.
/// </summary>
public interface ICurrentUser
{
    int? AgentId { get; }

    string? FullName { get; }
}
