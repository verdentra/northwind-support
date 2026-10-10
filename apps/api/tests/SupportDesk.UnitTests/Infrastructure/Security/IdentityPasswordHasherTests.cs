using SupportDesk.Infrastructure.Security;

namespace SupportDesk.UnitTests.Infrastructure.Security;

/// <summary>Runs the real hasher: these tests exercise the actual PBKDF2 implementation.</summary>
public class IdentityPasswordHasherTests
{
    private static readonly IdentityPasswordHasher Hasher = new();

    [Fact]
    public void Verify_TheRightPassword_Passes()
    {
        var hash = Hasher.Hash("LocalDev-Only-Pa55!");

        Assert.True(Hasher.Verify(hash, "LocalDev-Only-Pa55!"));
    }

    [Theory]
    [InlineData("localdev-only-pa55!")]
    [InlineData("LocalDev-Only-Pa55")]
    [InlineData("")]
    public void Verify_AnyOtherPassword_Fails(string attempt)
    {
        var hash = Hasher.Hash("LocalDev-Only-Pa55!");

        Assert.False(Hasher.Verify(hash, attempt));
    }

    [Fact]
    public void Hash_NeverContainsThePasswordAndIsSaltedEachTime()
    {
        var first = Hasher.Hash("LocalDev-Only-Pa55!");
        var second = Hasher.Hash("LocalDev-Only-Pa55!");

        Assert.DoesNotContain("LocalDev-Only-Pa55!", first);
        Assert.NotEqual(first, second);
        Assert.True(Hasher.Verify(second, "LocalDev-Only-Pa55!"));
    }

    [Fact]
    public void Verify_WithoutAStoredHash_Fails() =>
        Assert.False(Hasher.Verify(null, "LocalDev-Only-Pa55!"));

    [Fact]
    public void Verify_AStoredValueThatIsNotAHash_Fails() =>
        Assert.False(Hasher.Verify("not-a-hash", "not-a-hash"));
}
