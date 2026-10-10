namespace SupportDesk.Application.Exceptions;

/// <summary>
/// Thrown when the caller is not, or can no longer be, authenticated - for example wrong
/// credentials. Surfaces as HTTP 401. The message is deliberately generic.
/// </summary>
public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message)
    {
    }
}
