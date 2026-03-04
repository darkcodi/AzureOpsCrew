namespace AzureOpsCrew.Api.Email;

public interface IRegistrationEmailSender
{
    Task SendRegistrationCodeAsync(
        string recipientEmail,
        string verificationCode,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken);
}
