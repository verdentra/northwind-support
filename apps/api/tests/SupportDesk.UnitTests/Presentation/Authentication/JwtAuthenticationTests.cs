using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Presentation.Authentication;

namespace SupportDesk.UnitTests.Presentation.Authentication;

public sealed class JwtAuthenticationTests : IDisposable
{
    private const string Issuer = "test-issuer";
    private const string Audience = "test-audience";
    private const string SigningKey = "test-signing-key-with-at-least-32-bytes-long";

    private readonly TestServer _server;

    public JwtAuthenticationTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                ["Jwt:SigningKey"] = SigningKey,
                ["Jwt:TokenLifetimeMinutes"] = "60"
            })
            .Build();

        _server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddJwtAuthentication(configuration);
            })
            .Configure(application =>
            {
                application.UseRouting();
                application.UseAuthentication();
                application.UseAuthorization();
                application.UseEndpoints(endpoints =>
                    endpoints.MapGet("/protected", async context =>
                    {
                        await context.Response.WriteAsync("authenticated");
                    }));
            }));
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _server.CreateClient();

        var response = await client.GetAsync("/protected");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_Succeeds()
    {
        using var client = _server.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(DateTime.UtcNow.AddMinutes(10)));

        var response = await client.GetAsync("/protected");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("authenticated", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ProtectedEndpoint_WithExpiredOrInvalidSignatureToken_ReturnsUnauthorized()
    {
        using var client = _server.CreateClient();

        foreach (var token in new[]
        {
            CreateToken(DateTime.UtcNow.AddMinutes(-5)),
            CreateToken(DateTime.UtcNow.AddMinutes(10), "different-signing-key-with-at-least-32-bytes")
        })
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            using var response = await client.GetAsync("/protected");

            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    public void Dispose() => _server.Dispose();

    private static string CreateToken(DateTime expiresAtUtc, string? signingKey = null)
    {
        var now = DateTime.UtcNow;
        var notBefore = expiresAtUtc <= now ? expiresAtUtc.AddMinutes(-10) : now.AddMinutes(-1);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey ?? SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            Issuer,
            Audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, "42"),
                new Claim(JwtRegisteredClaimNames.Email, "agent@example.com")
            ],
            notBefore,
            expiresAtUtc,
            credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
