using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public class LoginCommandHandlerTests
{
    private const string Email = "sara.lindqvist@northwind-support.example";
    private const string Password = "correct horse battery staple";
    private const string StoredHash = "stored-hash";

    private readonly Mock<IAgentRepository> _agents = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenIssuer> _tokens = new();

    public LoginCommandHandlerTests()
    {
        _hasher.Setup(h => h.Verify(It.IsAny<string?>(), It.IsAny<string>())).Returns(false);
        _hasher.Setup(h => h.Verify(StoredHash, Password)).Returns(true);

        _tokens.Setup(t => t.Issue(It.IsAny<CurrentAgentDto>()))
            .Returns(new IssuedToken("signed.jwt.token", FixedClock.DefaultNow.AddHours(1)));
    }

    private LoginCommandHandler Handler() =>
        new(_agents.Object, _hasher.Object, _tokens.Object, NullLogger<LoginCommandHandler>.Instance);

    private void ExistingAgent(bool active = true, string? hash = StoredHash)
    {
        var agent = new Agent("Sara Lindqvist", Email, 6, FixedClock.DefaultNow);

        if (hash is not null)
        {
            agent.SetPasswordHash(hash);
        }

        if (!active)
        {
            agent.Deactivate();
        }

        _agents.Setup(a => a.GetByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
    }

    [Fact]
    public async Task HandleAsync_WithTheRightPassword_IssuesATokenAndGreetsTheAgent()
    {
        ExistingAgent();

        var response = await Handler().HandleAsync(new LoginRequest($"  {Email} ", Password), CancellationToken.None);

        Assert.Equal("signed.jwt.token", response.AccessToken);
        Assert.Equal(FixedClock.DefaultNow.AddHours(1), response.ExpiresAtUtc);
        Assert.Equal("Sara Lindqvist", response.Agent.FullName);
        Assert.Equal(Email, response.Agent.Email);
    }

    [Fact]
    public async Task HandleAsync_WithTheWrongPassword_IsUnauthorizedAndIssuesNoToken()
    {
        ExistingAgent();

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => Handler().HandleAsync(new LoginRequest(Email, "wrong password"), CancellationToken.None));

        Assert.Equal(LoginCommandHandler.InvalidCredentialsMessage, exception.Message);
        _tokens.Verify(t => t.Issue(It.IsAny<CurrentAgentDto>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithAnUnknownEmail_GivesTheSameAnswerAsAWrongPassword()
    {
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => Handler().HandleAsync(new LoginRequest("nobody@example.com", Password), CancellationToken.None));

        Assert.Equal(LoginCommandHandler.InvalidCredentialsMessage, exception.Message);
        // Still pays for a verification, so timing does not reveal that the account is missing.
        _hasher.Verify(h => h.Verify(null, Password), Times.Once);
        _tokens.Verify(t => t.Issue(It.IsAny<CurrentAgentDto>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ForAnInactiveAgent_IsUnauthorizedEvenWithTheRightPassword()
    {
        ExistingAgent(active: false);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => Handler().HandleAsync(new LoginRequest(Email, Password), CancellationToken.None));

        Assert.Equal(LoginCommandHandler.InvalidCredentialsMessage, exception.Message);
        _tokens.Verify(t => t.Issue(It.IsAny<CurrentAgentDto>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ForAnAgentWithoutCredentials_IsUnauthorized()
    {
        ExistingAgent(hash: null);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => Handler().HandleAsync(new LoginRequest(Email, Password), CancellationToken.None));
    }
}
