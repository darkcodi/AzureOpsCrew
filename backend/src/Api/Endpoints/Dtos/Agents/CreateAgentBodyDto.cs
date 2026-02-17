using AzureOpsCrew.Domain.Agents;

namespace AzureOpsCrew.Api.Endpoints.Dtos.Agents
{
    public record CreateAgentBodyDto(AgentInfo Info)
    {
        public int ClientId { get; set; }

        public Guid ProviderId { get; set; }

        public string Color { get; set; } = "#43b581";
    }
}
