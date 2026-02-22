using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AzureOpsCrew.Api.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace AzureOpsCrew.Api.Auth;

public sealed class KeycloakIdTokenValidator
{
    private readonly KeycloakOidcSettings _settings;
    private readonly ConfigurationManager<OpenIdConnectConfiguration>? _configurationManager;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public KeycloakIdTokenValidator(IOptions<KeycloakOidcSettings> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _settings = options.Value;
        if (!_settings.Enabled)
            return;

        if (string.IsNullOrWhiteSpace(_settings.Authority))
            throw new InvalidOperationException("KeycloakOidc__Authority is required when KeycloakOidc__Enabled=true.");

        if (string.IsNullOrWhiteSpace(_settings.ClientId))
            throw new InvalidOperationException("KeycloakOidc__ClientId is required when KeycloakOidc__Enabled=true.");

        var authority = _settings.Authority.TrimEnd('/');
        var metadataAddress = $"{authority}/.well-known/openid-configuration";
        var retriever = new HttpDocumentRetriever { RequireHttps = true };

        _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            retriever);
    }

    public bool IsEnabled => _settings.Enabled;

    public async Task<ClaimsPrincipal> ValidateIdTokenAsync(string idToken, CancellationToken cancellationToken)
    {
        if (!_settings.Enabled || _configurationManager is null)
            throw new InvalidOperationException("Keycloak OIDC validation is not enabled.");

        if (string.IsNullOrWhiteSpace(idToken))
            throw new SecurityTokenException("ID token is required.");

        var configuration = await _configurationManager.GetConfigurationAsync(cancellationToken);
        return Validate(idToken, configuration);
    }

    private ClaimsPrincipal Validate(string idToken, OpenIdConnectConfiguration configuration)
    {
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _settings.Authority.TrimEnd('/'),
            ValidateAudience = true,
            ValidAudience = _settings.ClientId,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = configuration.SigningKeys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            RequireSignedTokens = true,
            NameClaimType = "name"
        };

        try
        {
            return _tokenHandler.ValidateToken(idToken, validationParameters, out _);
        }
        catch (ArgumentException ex)
        {
            throw new SecurityTokenException("Invalid ID token format.", ex);
        }
        catch (SecurityTokenSignatureKeyNotFoundException)
        {
            _configurationManager!.RequestRefresh();
            throw;
        }
    }
}
