using System.Net.Http.Json;
using AzureOpsCrew.Api.Settings;
using Microsoft.Extensions.Options;

namespace AzureOpsCrew.Api.Email;

public sealed class BrevoRegistrationEmailSender : IRegistrationEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly BrevoSettings _settings;
    private readonly ILogger<BrevoRegistrationEmailSender> _logger;

    public BrevoRegistrationEmailSender(
        HttpClient httpClient,
        IOptions<BrevoSettings> settings,
        ILogger<BrevoRegistrationEmailSender> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendRegistrationCodeAsync(
        string recipientEmail,
        string verificationCode,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken)
    {
        var subject = "Your Azure Ops Crew security code";
        var expiresAt = expiresAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        var htmlContent =
            $"""
             <html>
               <body style="font-family:Arial,sans-serif;line-height:1.5;">
                 <p>Your Azure Ops Crew verification code is:</p>
                 <p style="font-size:28px;font-weight:700;letter-spacing:3px;margin:16px 0;">{verificationCode}</p>
                 <p>This code expires at {expiresAt}.</p>
                 <p>If you did not request this, you can safely ignore this email.</p>
               </body>
             </html>
             """;
        var textContent =
            $"Your Azure Ops Crew verification code is: {verificationCode}. This code expires at {expiresAt}.";

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v3/smtp/email")
        {
            Content = JsonContent.Create(new BrevoSendEmailRequest(
                Sender: new BrevoContact(_settings.SenderEmail, _settings.SenderName),
                To: [new BrevoContact(recipientEmail, null)],
                Subject: subject,
                HtmlContent: htmlContent,
                TextContent: textContent))
        };

        request.Headers.Add("api-key", _settings.ApiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return;

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError(
            "Brevo email send failed. StatusCode={StatusCode}. Response={Response}",
            (int)response.StatusCode,
            responseBody);

        throw new InvalidOperationException("Unable to send verification email.");
    }

    private sealed record BrevoContact(string Email, string? Name);

    private sealed record BrevoSendEmailRequest(
        BrevoContact Sender,
        BrevoContact[] To,
        string Subject,
        string HtmlContent,
        string TextContent);
}
