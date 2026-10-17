namespace SupportDesk.Application.Abstractions;

/// <summary>The authenticated identity associated with the current request.</summary>
public interface ICurrentUser
{
    string? Subject { get; }

    string? Email { get; }

    int? AgentId { get; }
}
