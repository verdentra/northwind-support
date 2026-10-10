namespace SupportDesk.Presentation.Authentication;

/// <summary>The claims an access token carries (registered JWT claim names).</summary>
public static class AgentClaims
{
    public const string AgentId = "sub";

    public const string FullName = "name";

    public const string Email = "email";

    public const string TokenId = "jti";
}
