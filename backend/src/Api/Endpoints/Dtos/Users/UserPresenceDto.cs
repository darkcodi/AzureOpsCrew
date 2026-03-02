namespace AzureOpsCrew.Api.Endpoints.Dtos.Users;

public sealed record UserPresenceDto(
    Guid Id,
    string DisplayName,
    bool IsOnline,
    bool IsCurrentUser,
    DateTime? LastSeenAtUtc);
