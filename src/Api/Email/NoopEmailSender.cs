namespace AzureOpsCrew.Api.Email;

public class NoopEmailSender : IRegistrationEmailSender, IDisposable
{
    private readonly HttpClient _httpClient;

    public NoopEmailSender(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task SendRegistrationCodeAsync(string recipientEmail, string verificationCode, DateTime expiresAtUtc,
        CancellationToken cancellationToken)
    {
        // No-op implementation for testing or when email sending is not configured.
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
