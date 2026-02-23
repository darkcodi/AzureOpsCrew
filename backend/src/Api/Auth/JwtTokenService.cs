using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AzureOpsCrew.Api.Settings;
using AzureOpsCrew.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AzureOpsCrew.Api.Auth;

public sealed class JwtTokenService
{
    private readonly JwtSettings _settings;
    private readonly byte[] _signingKey;

    public JwtTokenService(IOptions<JwtSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings.Value;
        if (string.IsNullOrWhiteSpace(_settings.SigningKey))
        {
            throw new ArgumentException("Jwt:SigningKey must be configured.", nameof(settings));
        }

        _signingKey = Encoding.UTF8.GetBytes(_settings.SigningKey);
        if (_signingKey.Length < 16)
        {
            throw new ArgumentException("Jwt:SigningKey must be at least 16 bytes.", nameof(settings));
        }
    }

    public AuthTokenResult CreateToken(User user)
    {
        var now = DateTime.UtcNow;
        var expiresAtUtc = now.AddMinutes(_settings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };

        var securityKey = new SymmetricSecurityKey(_signingKey);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var tokenValue = new JwtSecurityTokenHandler().WriteToken(token);
        return new AuthTokenResult(tokenValue, expiresAtUtc);
    }
}

public sealed record AuthTokenResult(string AccessToken, DateTime ExpiresAtUtc);
