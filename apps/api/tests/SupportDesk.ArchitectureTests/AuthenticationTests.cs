using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Presentation.Authentication;
using SupportDesk.Presentation.Controllers;

namespace SupportDesk.ArchitectureTests;

/// <summary>
/// Every endpoint is closed unless it explicitly opts out, and the tokens the API issues are the
/// ones it accepts. Checked against the real registrations, without a database or a web server.
/// </summary>
public class AuthenticationTests
{
    private const string SigningKey = "test-only-signing-key-that-is-long-enough-0123456789";

    [Fact]
    public void TheFallbackPolicy_RequiresAnAuthenticatedUser()
    {
        using var provider = Services(new FixedClock(DateTime.UtcNow));

        var fallback = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value.FallbackPolicy;

        Assert.NotNull(fallback);
        Assert.Contains(fallback.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public void OnlyLogin_AllowsAnonymousAccess()
    {
        var anonymous = typeof(AuthController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => t.GetCustomAttribute<AllowAnonymousAttribute>() is not null ||
                            m.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
                .Select(m => $"{t.Name}.{m.Name}"))
            .ToList();

        Assert.Equal(new[] { "AuthController.Login" }, anonymous);
    }

    [Fact]
    public async Task AnIssuedToken_IsAcceptedAndCarriesTheAgent()
    {
        using var provider = Services(new FixedClock(DateTime.UtcNow));

        var token = provider.GetRequiredService<ITokenIssuer>()
            .Issue(new CurrentAgentDto(4, "Sara Lindqvist", "sara@example.com"));

        var result = await Validate(provider, token.AccessToken);

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal("4", result.Claims[AgentClaims.AgentId]);
        Assert.Equal("Sara Lindqvist", result.Claims[AgentClaims.FullName]);
    }

    [Fact]
    public async Task AnExpiredToken_IsRejected()
    {
        // Issued two hours ago with a 60-minute lifetime.
        using var provider = Services(new FixedClock(DateTime.UtcNow.AddHours(-2)));

        var token = provider.GetRequiredService<ITokenIssuer>()
            .Issue(new CurrentAgentDto(4, "Sara Lindqvist", "sara@example.com"));

        Assert.False((await Validate(provider, token.AccessToken)).IsValid);
    }

    [Fact]
    public async Task ATamperedToken_IsRejected()
    {
        using var provider = Services(new FixedClock(DateTime.UtcNow));

        var token = provider.GetRequiredService<ITokenIssuer>()
            .Issue(new CurrentAgentDto(4, "Sara Lindqvist", "sara@example.com")).AccessToken;
        var tampered = token[..^4] + (token.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");

        Assert.False((await Validate(provider, tampered)).IsValid);
    }

    [Fact]
    public void AShortSigningKey_FailsValidation()
    {
        var options = new JwtOptions { SigningKey = "too-short" };

        Assert.Contains(options.Validate(), e => e.Contains("SigningKey", StringComparison.Ordinal));
    }

    private static ServiceProvider Services(IClock clock)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = SigningKey,
                ["Jwt:TokenLifetimeMinutes"] = "60"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(clock);
        services.AddApiAuthentication(configuration);

        return services.BuildServiceProvider();
    }

    private static Task<Microsoft.IdentityModel.Tokens.TokenValidationResult> Validate(IServiceProvider provider, string token)
    {
        var bearer = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        return new JsonWebTokenHandler().ValidateTokenAsync(token, bearer.TokenValidationParameters);
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
