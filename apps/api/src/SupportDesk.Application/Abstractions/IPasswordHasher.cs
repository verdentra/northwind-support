namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Hashes and verifies passwords with a vetted, salted, deliberately slow algorithm. The
/// implementation lives in Infrastructure; nothing else ever handles a password.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// True when <paramref name="password"/> matches <paramref name="passwordHash"/>. A null hash
    /// still costs one full verification, so an unknown account takes as long to reject as a
    /// wrong password.
    /// </summary>
    bool Verify(string? passwordHash, string password);
}
