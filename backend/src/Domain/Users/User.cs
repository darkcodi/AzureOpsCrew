#pragma warning disable CS8618

namespace AzureOpsCrew.Domain.Users;

public sealed class User
{
    private User()
    {
    }

    public User(string email, string normalizedEmail, string passwordHash, string displayName)
    {
        Id = Guid.NewGuid();
        Email = email;
        NormalizedEmail = normalizedEmail;
        PasswordHash = passwordHash;
        DisplayName = displayName;
    }

    public Guid Id { get; set; }
    public string Email { get; private set; }
    public string NormalizedEmail { get; private set; }
    public string PasswordHash { get; private set; }
    public string DisplayName { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime DateCreated { get; private set; } = DateTime.UtcNow;
    public DateTime? DateModified { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    public void UpdateDisplayName(string displayName)
    {
        DisplayName = displayName;
        DateModified = DateTime.UtcNow;
    }

    public void UpdatePasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
        DateModified = DateTime.UtcNow;
    }

    public void MarkLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        DateModified = DateTime.UtcNow;
    }
}
