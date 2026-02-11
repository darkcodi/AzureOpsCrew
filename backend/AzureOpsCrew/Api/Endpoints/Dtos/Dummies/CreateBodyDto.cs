namespace AzureOpsCrew.Api.Endpoints.Dtos.Dummies
{
    public record CreateBodyDto
    {
        public string Name { get; init; } = "";

        public string? Description { get; init; }
    }
}
