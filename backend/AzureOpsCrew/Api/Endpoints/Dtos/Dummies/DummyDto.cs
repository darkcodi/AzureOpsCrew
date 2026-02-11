namespace AzureOpsCrew.Api.Endpoints.Dtos.Dummies
{
    public record DummyDto
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = "";

        public string? Description { get; init; }
    }
}
