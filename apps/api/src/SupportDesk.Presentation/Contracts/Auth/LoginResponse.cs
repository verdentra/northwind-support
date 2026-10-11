namespace SupportDesk.Presentation.Contracts.Auth;

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    AuthenticatedAgent User);

public sealed record AuthenticatedAgent(int Id, string FullName, string Email);
