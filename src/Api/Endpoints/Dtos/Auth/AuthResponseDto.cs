namespace AzureOpsCrew.Api.Endpoints.Dtos.Auth;

public sealed record AuthResponseDto(
    string AccessToken,
    DateTime ExpiresAtUtc,
    AuthUserDto User);
