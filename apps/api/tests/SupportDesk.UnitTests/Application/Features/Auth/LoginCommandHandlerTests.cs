using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.Presentation.Controllers;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public sealed class LoginCommandHandlerTests
{
    private const string Password = "DevOnly-Password1!";

    [Fact]
    public async Task HandleAsync_WithCorrectPassword_ReturnsTokenAndAgentWithoutPlaintextPassword()
    {
        var hasher = new PasswordHasher<Agent>();
        var agent = NewAgent();
        agent.SetPasswordHash(hasher.HashPassword(agent, Password));
        var agents = new Mock<IAgentRepository>();
        agents.Setup(repository => repository.GetByEmailAsync(agent.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        var tokenIssuer = new Mock<IJwtTokenIssuer>();
        tokenIssuer.Setup(issuer => issuer.Issue(agent))
            .Returns(new IssuedJwt("signed-token", FixedClock.DefaultNow.AddMinutes(60)));
        var handler = new LoginCommandHandler(agents.Object, hasher, tokenIssuer.Object);

        var result = await handler.HandleAsync(
            new LoginRequest { Email = agent.Email, Password = Password },
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("signed-token", result.AccessToken);
        Assert.Equal(agent.Id, result.User.Id);
        Assert.Equal(agent.FullName, result.User.FullName);
        Assert.Equal(agent.Email, result.User.Email);
        Assert.NotEqual(Password, agent.PasswordHash);
        Assert.Equal(
            PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(agent, agent.PasswordHash!, Password));
    }

    [Fact]
    public async Task HandleAsync_WithWrongPassword_ReturnsNoToken()
    {
        var hasher = new PasswordHasher<Agent>();
        var agent = NewAgent();
        agent.SetPasswordHash(hasher.HashPassword(agent, Password));
        var agents = new Mock<IAgentRepository>();
        agents.Setup(repository => repository.GetByEmailAsync(agent.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        var tokenIssuer = new Mock<IJwtTokenIssuer>();
        var handler = new LoginCommandHandler(agents.Object, hasher, tokenIssuer.Object);

        var result = await handler.HandleAsync(
            new LoginRequest { Email = agent.Email, Password = "Wrong-Password" },
            CancellationToken.None);

        Assert.Null(result);
        tokenIssuer.Verify(issuer => issuer.Issue(It.IsAny<Agent>()), Times.Never());
    }

    [Fact]
    public async Task HandleAsync_ForInactiveAgent_ReturnsNoToken()
    {
        var hasher = new PasswordHasher<Agent>();
        var agent = NewAgent();
        agent.Deactivate();
        agent.SetPasswordHash(hasher.HashPassword(agent, Password));
        var agents = new Mock<IAgentRepository>();
        agents.Setup(repository => repository.GetByEmailAsync(agent.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        var tokenIssuer = new Mock<IJwtTokenIssuer>();
        var handler = new LoginCommandHandler(agents.Object, hasher, tokenIssuer.Object);

        var result = await handler.HandleAsync(
            new LoginRequest { Email = agent.Email, Password = Password },
            CancellationToken.None);

        Assert.Null(result);
        tokenIssuer.Verify(issuer => issuer.Issue(It.IsAny<Agent>()), Times.Never());
    }

    [Fact]
    public async Task Login_UnknownEmailAndWrongPassword_ReturnSameGenericUnauthorizedResponse()
    {
        var hasher = new PasswordHasher<Agent>();
        var agent = NewAgent();
        agent.SetPasswordHash(hasher.HashPassword(agent, Password));

        var unknownEmailAgents = new Mock<IAgentRepository>();
        unknownEmailAgents.Setup(repository => repository.GetByEmailAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);
        var unknownEmailTokenIssuer = new Mock<IJwtTokenIssuer>();
        var unknownEmailHandler = new LoginCommandHandler(
            unknownEmailAgents.Object,
            hasher,
            unknownEmailTokenIssuer.Object);

        var wrongPasswordAgents = new Mock<IAgentRepository>();
        wrongPasswordAgents.Setup(repository => repository.GetByEmailAsync(
                agent.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        var wrongPasswordTokenIssuer = new Mock<IJwtTokenIssuer>();
        var wrongPasswordHandler = new LoginCommandHandler(
            wrongPasswordAgents.Object,
            hasher,
            wrongPasswordTokenIssuer.Object);

        var inactiveAgent = NewAgent();
        inactiveAgent.Deactivate();
        inactiveAgent.SetPasswordHash(hasher.HashPassword(inactiveAgent, Password));
        var inactiveAgents = new Mock<IAgentRepository>();
        inactiveAgents.Setup(repository => repository.GetByEmailAsync(
                inactiveAgent.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveAgent);
        var inactiveHandler = new LoginCommandHandler(
            inactiveAgents.Object,
            hasher,
            Mock.Of<IJwtTokenIssuer>());

        var unknownResponse = await LoginFailureAsync(unknownEmailHandler, "missing@example.com", Password);
        var wrongPasswordResponse = await LoginFailureAsync(wrongPasswordHandler, agent.Email, "wrong-password");
        var inactiveResponse = await LoginFailureAsync(inactiveHandler, inactiveAgent.Email, Password);

        Assert.Equal(401, unknownResponse.StatusCode);
        Assert.Equal(unknownResponse.StatusCode, wrongPasswordResponse.StatusCode);
        Assert.Equal(unknownResponse.StatusCode, inactiveResponse.StatusCode);
        Assert.Equal(401, unknownResponse.Problem.Status);
        Assert.Equal(unknownResponse.Problem.Status, wrongPasswordResponse.Problem.Status);
        Assert.Equal(unknownResponse.Problem.Title, wrongPasswordResponse.Problem.Title);
        Assert.Equal(unknownResponse.Problem.Detail, wrongPasswordResponse.Problem.Detail);
        Assert.Equal(unknownResponse.Problem.Detail, inactiveResponse.Problem.Detail);
        Assert.Equal("Invalid email or password.", unknownResponse.Problem.Detail);
        unknownEmailTokenIssuer.Verify(issuer => issuer.Issue(It.IsAny<Agent>()), Times.Never());
        wrongPasswordTokenIssuer.Verify(issuer => issuer.Issue(It.IsAny<Agent>()), Times.Never());
    }

    private static Agent NewAgent() =>
        new("Alex Turner", "alex.turner@example.com", 10, FixedClock.DefaultNow);

    private static async Task<(int StatusCode, ProblemDetails Problem)> LoginFailureAsync(
        LoginCommandHandler handler,
        string email,
        string password)
    {
        var controller = new AuthController(
            handler,
            Mock.Of<ICurrentUser>(),
            Mock.Of<IAgentRepository>());

        var action = await controller.Login(
            new LoginRequest { Email = email, Password = password },
            CancellationToken.None);
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(action.Result);
        Assert.Equal(401, unauthorized.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(unauthorized.Value);
        return (unauthorized.StatusCode!.Value, problem);
    }
}
