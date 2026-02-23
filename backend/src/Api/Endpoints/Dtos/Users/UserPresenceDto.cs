namespace AzureOpsCrew.Api.Endpoints.Dtos.Users;

public sealed record UserPresenceDto(
    int Id,
    string DisplayName,
    bool IsOnline,
    bool IsCurrentUser,
    DateTime? LastSeenAtUtc);
