#pragma warning disable CS8618

namespace AzureOpsCrew.Domain.Users;

public sealed class PendingRegistration
{
    private PendingRegistration()
    {
    }

    public PendingRegistration(string email, string normalizedEmail)
    {
        Email = email;
        NormalizedEmail = normalizedEmail;
    }

    public int Id { get; private set; }
    public string Email { get; private set; }
    public string NormalizedEmail { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string VerificationCodeHash { get; private set; } = string.Empty;
    public DateTime VerificationCodeExpiresAt { get; private set; }
    public DateTime VerificationCodeSentAt { get; private set; }
    public int VerificationAttempts { get; private set; }
    public DateTime DateCreated { get; private set; } = DateTime.UtcNow;
    public DateTime? DateModified { get; private set; }

    public void Refresh(
        string email,
        string displayName,
        string passwordHash,
        string verificationCodeHash,
        DateTime codeExpiresAtUtc,
        DateTime codeSentAtUtc)
    {
        Email = email;
        DisplayName = displayName;
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
