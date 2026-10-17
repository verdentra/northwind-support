namespace SupportDesk.Application.Contracts.Auth;

public sealed record LoginResponseDto(
    string AccessToken,
    DateTime ExpiresAtUtc,
    AgentIdentityDto User);
