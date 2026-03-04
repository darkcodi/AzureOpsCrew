#pragma warning disable CS8618

namespace AzureOpsCrew.Domain.Users;

public sealed class User
{
    private User()
    {
    }

    public User(Guid id, string email, string normalizedEmail, string passwordHash, string username, string normalizedUsername)
    {
        Id = id;
        Email = email;
        NormalizedEmail = normalizedEmail;
        PasswordHash = passwordHash;
        Username = username;
        NormalizedUsername = normalizedUsername;
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; }
    public string NormalizedEmail { get; private set; }
    public string PasswordHash { get; private set; }
    public string Username { get; private set; }
    public string NormalizedUsername { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime DateCreated { get; private set; } = DateTime.UtcNow;
    public DateTime? DateModified { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    public void UpdateUsername(string username, string normalizedUsername)
    {
        Username = username;
        NormalizedUsername = normalizedUsername;
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
