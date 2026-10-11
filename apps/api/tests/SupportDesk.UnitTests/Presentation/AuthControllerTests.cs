using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.Presentation.Auth;
using SupportDesk.Presentation.Contracts.Auth;
using SupportDesk.Presentation.Controllers;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Presentation;

public sealed class AuthControllerTests
{
    private const string Password = "LocalDev123!";

    private static readonly JwtOptions Options = new()
    {
        Issuer = "NorthwindSupport.Tests",
        Audience = "NorthwindSupport.Web.Tests",
        SigningKey = "test-signing-key-which-is-at-least-32-bytes-long",
        AccessTokenMinutes = 60
    };

    [Fact]
    public async Task Login_WithValidActiveAgent_ReturnsTokenAndUser()
    {
        var agent = NewAgent();
        agent.SetPasswordHash(new PasswordHasher<Agent>().HashPassword(agent, Password));
        var controller = CreateController(agent);

        var response = await controller.Login(
            new LoginRequest { Email = agent.Email, Password = Password },
            CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(response.Result);
        var login = Assert.IsType<LoginResponse>(result.Value);

        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.Equal(agent.Id, login.User.Id);
        Assert.Equal(agent.FullName, login.User.FullName);
        Assert.Equal(agent.Email, login.User.Email);
        Assert.Equal(FixedClock.DefaultNow.AddMinutes(60), login.ExpiresAtUtc);
    }

    [Fact]
    public async Task Login_UnknownEmailAndWrongPassword_ReturnSameUnauthorizedMessage()
    {
        var agent = NewAgent();
        agent.SetPasswordHash(new PasswordHasher<Agent>().HashPassword(agent, Password));
        var repository = new Mock<IAgentRepository>();
        repository.Setup(repo => repo.GetByEmailAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string email, CancellationToken _) =>
                email == agent.Email ? agent : null);
        var controller = CreateController(repository.Object);

        var unknownEmail = await controller.Login(
            new LoginRequest { Email = "unknown@example.com", Password = Password },
            CancellationToken.None);
        var wrongPassword = await controller.Login(
            new LoginRequest { Email = agent.Email, Password = "incorrect" },
            CancellationToken.None);

        var unknownProblem = Assert.IsType<ProblemDetails>(
            Assert.IsType<UnauthorizedObjectResult>(unknownEmail.Result).Value);
        var passwordProblem = Assert.IsType<ProblemDetails>(
            Assert.IsType<UnauthorizedObjectResult>(wrongPassword.Result).Value);

        Assert.Equal(unknownProblem.Status, passwordProblem.Status);
        Assert.Equal(unknownProblem.Detail, passwordProblem.Detail);
    }

    [Fact]
    public async Task Login_InactiveAgentIsRejected()
    {
        var agent = NewAgent(active: false);
        agent.SetPasswordHash(new PasswordHasher<Agent>().HashPassword(agent, Password));
        var controller = CreateController(agent);

        var response = await controller.Login(
            new LoginRequest { Email = agent.Email, Password = Password },
            CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(response.Result);
    }

    [Fact]
    public void Me_ReturnsIdentityFromAuthenticatedClaims()
    {
        var agent = NewAgent();
        var controller = CreateController(agent);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, agent.Id.ToString()),
                    new Claim(ClaimTypes.Name, agent.FullName),
                    new Claim(ClaimTypes.Email, agent.Email)
                ], "Bearer"))
            }
        };

        var response = controller.Me();

        var result = Assert.IsType<OkObjectResult>(response.Result);
        var user = Assert.IsType<AuthenticatedAgent>(result.Value);
        Assert.Equal(agent.Id, user.Id);
        Assert.Equal(agent.FullName, user.FullName);
        Assert.Equal(agent.Email, user.Email);
    }

    private static Agent NewAgent(bool active = true)
    {
        var agent = new Agent(
            "Priya Nair",
            "priya.nair@example.com",
            8,
            FixedClock.DefaultNow);
        EntityId.WithId(agent, 3);

        if (!active)
        {
            agent.Deactivate();
        }

        return agent;
    }

    private static AuthController CreateController(Agent? agent)
    {
        var repository = new Mock<IAgentRepository>();
        repository.Setup(repo => repo.GetByEmailAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        return CreateController(repository.Object);
    }

    private static AuthController CreateController(IAgentRepository repository) =>
        new(
            repository,
            new PasswordHasher<Agent>(),
            Microsoft.Extensions.Options.Options.Create(Options),
            new FixedClock());
}
