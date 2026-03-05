namespace AzureOpsCrew.Api.Endpoints.Dtos.Auth;

public sealed record RegisterChallengeDto(
    string Message,
    DateTime ExpiresAtUtc,
    int ResendAvailableInSeconds);
