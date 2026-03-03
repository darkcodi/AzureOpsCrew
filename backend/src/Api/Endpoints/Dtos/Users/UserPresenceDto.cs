namespace AzureOpsCrew.Api.Endpoints.Dtos.Users;

public sealed record UserPresenceDto(
    Guid Id,
    string Username,
    bool IsOnline,
    bool IsCurrentUser,
    DateTime? LastSeenAtUtc);
