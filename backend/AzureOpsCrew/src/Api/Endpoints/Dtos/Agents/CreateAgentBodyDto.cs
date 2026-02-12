using AzureOpsCrew.Domain.Agents;

namespace AzureOpsCrew.Api.Endpoints.Dtos.Agents
{
    public class CreateAgentBodyDto
    {
        public int ClientId { get; set; }
        public AgentInfo? Info { get; set; }
        public Provider Provider { get; set; }
    }
}
