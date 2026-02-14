#pragma warning disable CS8618

namespace AzureOpsCrew.Domain.Agents
{
    public class Agent
    {
        private Agent()
        {
        }

        public Agent(Guid id, int clientId, AgentInfo info, Provider provider, string providerAgentId, string color)
        {
            Id = id;
            ClientId = clientId;
            Info = info;
            Provider = provider;
            ProviderAgentId = providerAgentId;
            Color = color;
        }

        public Guid Id { get; private set; }

        public string ProviderAgentId { get; private set; }

        public int ClientId { get; private set; }

        public AgentInfo Info { get; private set; }

        public Provider Provider { get; private set; }

        public string Color { get; private set; } = "#43b581";

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    }
}
