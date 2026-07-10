namespace eProcure.Api.Auth;

/// <summary>JWT signing/validation settings, bound from the "Jwt" config section.</summary>
public sealed class JwtOptions
{
    public string Issuer { get; set; } = "eprocure";
    public string Audience { get; set; } = "eprocure";
    public string Key { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 480;
}
