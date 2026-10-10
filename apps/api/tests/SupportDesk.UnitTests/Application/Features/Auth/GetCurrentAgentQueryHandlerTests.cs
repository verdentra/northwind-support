using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Queries.GetCurrentAgent;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public class GetCurrentAgentQueryHandlerTests
{
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IAgentRepository> _agents = new();

    private GetCurrentAgentQueryHandler Handler() => new(_currentUser.Object, _agents.Object);

    private void Agent(int id, bool active)
    {
        var agent = new Agent("Ben Osei", "ben.osei@northwind-support.example", 8, FixedClock.DefaultNow);

        if (!active)
        {
            agent.Deactivate();
        }

        _agents.Setup(a => a.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
    }

    [Fact]
    public async Task HandleAsync_ReturnsTheSignedInAgent()
    {
        _currentUser.Setup(u => u.AgentId).Returns(2);
        Agent(2, active: true);

        var me = await Handler().HandleAsync(CancellationToken.None);

        Assert.Equal("Ben Osei", me.FullName);
        Assert.Equal("ben.osei@northwind-support.example", me.Email);
    }

    [Fact]
    public async Task HandleAsync_WhenNotSignedIn_IsUnauthorized() =>
        await Assert.ThrowsAsync<UnauthorizedException>(() => Handler().HandleAsync(CancellationToken.None));

    [Fact]
    public async Task HandleAsync_WhenTheAgentWasDeactivatedAfterSigningIn_IsUnauthorized()
    {
        _currentUser.Setup(u => u.AgentId).Returns(2);
        Agent(2, active: false);

        await Assert.ThrowsAsync<UnauthorizedException>(() => Handler().HandleAsync(CancellationToken.None));
    }
}
