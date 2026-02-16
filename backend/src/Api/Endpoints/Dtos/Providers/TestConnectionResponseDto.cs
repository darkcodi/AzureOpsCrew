namespace AzureOpsCrew.Api.Endpoints.Dtos.Providers;

public record TestConnectionResponseDto(bool Success, string? Message = null, string? ErrorType = null);
