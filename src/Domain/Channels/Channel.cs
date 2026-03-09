#pragma warning disable CS8618

namespace AzureOpsCrew.Domain.Channels
{
    public class Channel
    {
        private Channel()
        {
        }

        public Channel(Guid id, string name)
        {
            Id = id;
            Name = name;
        }

        public Guid Id { get; set; }

        public string Name { get; set; }

        public string? Description { get; set; }

        public string? ConversationId { get; set; }


        public Guid[] AgentIds { get; set; } = [];

        public Guid? ManagerAgentId { get; set; }

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        public bool IsOrchestrated => ManagerAgentId.HasValue;

        public void AddAgent(Guid agentId)
        {
            AgentIds = AgentIds.Concat([agentId]).ToArray();
        }

        public void RemoveAgent(Guid agentId)
        {
            AgentIds = AgentIds.Except([agentId]).ToArray();
        }
    }
}
