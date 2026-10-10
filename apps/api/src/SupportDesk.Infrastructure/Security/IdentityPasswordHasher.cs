using Microsoft.AspNetCore.Identity;
using IApplicationPasswordHasher = SupportDesk.Application.Abstractions.IPasswordHasher;

namespace SupportDesk.Infrastructure.Security;

/// <summary>
/// ASP.NET Core Identity's <see cref="PasswordHasher{TUser}"/>: PBKDF2 with HMAC-SHA512, a random
/// 128-bit salt per password and 100,000 iterations, with the algorithm and parameters stored in
/// the hash so they can be raised later without breaking existing passwords. Used on its own,
/// without the rest of Identity.
/// </summary>
public sealed class IdentityPasswordHasher : IApplicationPasswordHasher
{
    private static readonly HashSubject Subject = new();

    private readonly PasswordHasher<HashSubject> _hasher = new();

    /// <summary>A real hash of a random value, so verifying an unknown account costs the same as a wrong password.</summary>
    private readonly string _dummyHash;

    public IdentityPasswordHasher()
    {
        _dummyHash = _hasher.HashPassword(Subject, Guid.NewGuid().ToString("N"));
    }

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        return _hasher.HashPassword(Subject, password);
    }

    public bool Verify(string? passwordHash, string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        try
        {
            var result = _hasher.VerifyHashedPassword(Subject, passwordHash ?? _dummyHash, password);

            return passwordHash is not null && result is not PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            // A stored value that is not a hash at all never matches.
            return false;
        }
    }

    /// <summary>The Identity hasher is generic over a user type it does not actually use.</summary>
    private sealed class HashSubject;
}
