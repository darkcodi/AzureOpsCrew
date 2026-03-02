namespace AzureOpsCrew.Api.Endpoints.Dtos.Auth;

public sealed record AuthUserDto(Guid Id, string Email, string DisplayName);
