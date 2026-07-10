using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using eProcure.Application.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace eProcure.Api.Auth;

/// <summary>Issues signed JWTs for dev login with id, name and role claims.</summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, IClock clock)
{
    private readonly JwtOptions _opt = options.Value;

    public (string Token, DateTime ExpiresUtc) Issue(DevUser user)
    {
        var now = clock.UtcNow;
        var expires = now.AddMinutes(_opt.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Name),
            new(JwtRegisteredClaimNames.Email, user.Email),
        };
        claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
