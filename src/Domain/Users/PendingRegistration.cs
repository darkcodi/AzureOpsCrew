#pragma warning disable CS8618

namespace AzureOpsCrew.Domain.Users;

public sealed class PendingRegistration
{
    private PendingRegistration()
    {
    }

    public PendingRegistration(string email, string normalizedEmail, string username, string normalizedUsername)
    {
        Email = email;
        NormalizedEmail = normalizedEmail;
        Username = username;
        NormalizedUsername = normalizedUsername;
    }

    public int Id { get; private set; }
    public string Email { get; private set; }
    public string NormalizedEmail { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string NormalizedUsername { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string VerificationCodeHash { get; private set; } = string.Empty;
    public DateTime VerificationCodeExpiresAt { get; private set; }
    public DateTime VerificationCodeSentAt { get; private set; }
    public int VerificationAttempts { get; private set; }
    public DateTime DateCreated { get; private set; } = DateTime.UtcNow;
    public DateTime? DateModified { get; private set; }

    public void Refresh(
        string email,
        string username,
        string normalizedUsername,
        string passwordHash,
        string verificationCodeHash,
        DateTime codeExpiresAtUtc,
        DateTime codeSentAtUtc)
    {
        Email = email;
        Username = username;
        NormalizedUsername = normalizedUsername;
        PasswordHash = passwordHash;
        VerificationCodeHash = verificationCodeHash;
        VerificationCodeExpiresAt = codeExpiresAtUtc;
        VerificationCodeSentAt = codeSentAtUtc;
        VerificationAttempts = 0;
        DateModified = DateTime.UtcNow;
    }

    public void IncrementFailedAttempt()
    {
        VerificationAttempts += 1;
        DateModified = DateTime.UtcNow;
    }
}
