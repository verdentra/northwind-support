using System.Text;

namespace SupportDesk.Presentation.Authentication;

/// <summary>
/// The <c>Jwt</c> configuration section. The signing key is never in code: it comes from
/// configuration or the <c>Jwt__SigningKey</c> environment variable (see .env.example).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 needs at least 256 bits of key.</summary>
    public const int MinimumKeyBytes = 32;

    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "northwind-support";

    public string Audience { get; set; } = "northwind-support-web";

    /// <summary>How long an access token is accepted. There are no refresh tokens: sign in again after it.</summary>
    public int TokenLifetimeMinutes { get; set; } = 60;

    public byte[] SigningKeyBytes() => Encoding.UTF8.GetBytes(SigningKey);

    /// <summary>Everything wrong with these values; empty when they are usable.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (SigningKeyBytes().Length < MinimumKeyBytes)
        {
            errors.Add(
                $"{SectionName}:{nameof(SigningKey)} must be at least {MinimumKeyBytes} bytes. " +
                "Set it in configuration or the Jwt__SigningKey environment variable.");
        }

        if (string.IsNullOrWhiteSpace(Issuer))
        {
            errors.Add($"{SectionName}:{nameof(Issuer)} is required.");
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            errors.Add($"{SectionName}:{nameof(Audience)} is required.");
        }

        if (TokenLifetimeMinutes is < 1 or > 24 * 60)
        {
            errors.Add($"{SectionName}:{nameof(TokenLifetimeMinutes)} must be between 1 and 1440.");
        }

        return errors;
    }
}
