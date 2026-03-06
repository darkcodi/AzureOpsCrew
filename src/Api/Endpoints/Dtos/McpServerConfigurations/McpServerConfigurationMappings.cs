using AzureOpsCrew.Domain.McpServerConfigurations;
namespace AzureOpsCrew.Api.Endpoints.Dtos.McpServerConfigurations;
public static class McpServerConfigurationMappings
{
    public static McpServerConfigurationResponseDto ToResponseDto(this McpServerConfiguration configuration)
    {
        return new McpServerConfigurationResponseDto
        {
            Id = configuration.Id,
            Name = configuration.Name,
            Description = configuration.Description,
            Url = configuration.Url,
            IsEnabled = configuration.IsEnabled,
            ToolsSyncedAt = configuration.ToolsSyncedAt,
            DateCreated = configuration.DateCreated,
            Auth = new McpServerConfigurationAuthResponseDto
            {
                Type = configuration.Auth.Type.ToString(),
                HasBearerToken = !string.IsNullOrWhiteSpace(configuration.Auth.BearerToken),
                HasCustomHeaders = configuration.Auth.Headers.Count > 0,
                CustomHeaderNames = configuration.Auth.Headers
                    .Select(x => x.Name)
                    .ToArray(),
            },
            Tools = configuration.Tools
                .Select(tool => new McpServerToolConfigurationResponseDto
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    InputSchemaJson = tool.InputSchemaJson,
                    OutputSchemaJson = tool.OutputSchemaJson,
                    IsEnabled = tool.IsEnabled,
                })
                .ToArray()
        };
    }
    public static McpServerConfigurationResponseDto[] ToResponseDtoArray(this IEnumerable<McpServerConfiguration> configurations)
    {
        return configurations.Select(ToResponseDto).ToArray();
    }
}
