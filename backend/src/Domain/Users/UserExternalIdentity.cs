#pragma warning disable CS8618

namespace AzureOpsCrew.Domain.Users;

public sealed class UserExternalIdentity
{
    private UserExternalIdentity()
    {
    }

    public UserExternalIdentity(int userId, string provider, string providerSubject, string? email)
    {
        UserId = userId;
        Provider = provider;
        ProviderSubject = providerSubject;
        Email = email;
    }

    public int Id { get; private set; }
    public int UserId { get; private set; }
    public string Provider { get; private set; }
    public string ProviderSubject { get; private set; }
    public string? Email { get; private set; }
    public DateTime DateCreated { get; private set; } = DateTime.UtcNow;
    public DateTime? DateModified { get; private set; }

    public void UpdateEmail(string? email)
    {
        if (string.Equals(Email, email, StringComparison.Ordinal))
            return;

        Email = email;
        DateModified = DateTime.UtcNow;
    }
}
