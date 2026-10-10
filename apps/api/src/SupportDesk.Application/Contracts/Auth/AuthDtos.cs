namespace SupportDesk.Application.Contracts.Auth;

/// <summary>Body of POST /api/auth/login.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>The signed-in agent: enough to greet them and to stamp their actions.</summary>
public sealed record CurrentAgentDto(int Id, string FullName, string Email);

/// <summary>Response of POST /api/auth/login.</summary>
/// <param name="AccessToken">Send as <c>Authorization: Bearer {token}</c>.</param>
/// <param name="ExpiresAtUtc">When the token stops being accepted; sign in again after it.</param>
/// <param name="Agent">Who signed in.</param>
public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, CurrentAgentDto Agent);
