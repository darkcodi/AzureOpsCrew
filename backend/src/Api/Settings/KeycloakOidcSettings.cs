namespace AzureOpsCrew.Api.Settings;

public sealed class KeycloakOidcSettings
{
    public bool Enabled { get; set; }
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public bool RequireVerifiedEmail { get; set; } = true;
}
