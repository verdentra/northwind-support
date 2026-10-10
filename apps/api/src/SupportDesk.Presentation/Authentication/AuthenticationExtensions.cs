using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Presentation.Authentication;

public static class AuthenticationExtensions
{
    /// <summary>
    /// JWT bearer authentication, plus a fallback authorization policy that requires a signed-in
    /// user on every endpoint that does not explicitly allow anonymous access. New endpoints are
    /// therefore closed by default; only the login endpoint opts out. Swagger is middleware, not
    /// an endpoint, so it stays reachable.
    /// </summary>
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configured from the validated options rather than read here, so a missing key fails
        // start-up with the validator's message.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                var settings = jwt.Value;

                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(settings.SigningKeyBytes()),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = AgentClaims.FullName
                };

                // The same problem-document shape as every other error, instead of an empty 401.
                bearer.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();

                        var problem = new ProblemDetails
                        {
                            Title = "Unauthorized",
                            Detail = "Sign in to continue.",
                            Status = StatusCodes.Status401Unauthorized
                        };
                        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(problem);
                    }
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build());

        return services;
    }

    private sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
    {
        public ValidateOptionsResult Validate(string? name, JwtOptions options)
        {
            var errors = options.Validate();

            return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
        }
    }
}
