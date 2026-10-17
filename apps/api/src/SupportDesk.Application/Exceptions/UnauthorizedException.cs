namespace SupportDesk.Application.Exceptions;

/// <summary>An authenticated request cannot be associated with a valid agent identity.</summary>
public sealed class UnauthorizedException(string message) : Exception(message)
{
}
