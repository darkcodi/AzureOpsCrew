using AzureOpsCrew.Domain.Agents;

namespace AzureOpsCrew.Api.Endpoints.Dtos.Agents
{
    public record UpdateAgentBodyDto(AgentInfo Info)
    {
        public Guid ProviderId { get; set; }

        public string Color { get; set; } = "#43b581";
    }
}
