#pragma warning disable CS8618

namespace AzureOpsCrew.Domain.Channels
{
    public class Channel
    {
        private Channel()
        {
        }

        public Channel(Guid id, int clientId, string name)
        {
            Id = id;
            ClientId = clientId;
            Name = name;
        }

        public Guid Id { get; set; }

        public int ClientId { get; set; }

        public string Name { get; set; }

        public string? Description { get; set; }

        public string? ConversationId { get; set; }


        public string[] AgentIds { get; set; } = [];

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        public void AddAgent(string agentId)
        {
            AgentIds = AgentIds.Concat([agentId]).ToArray();
        }

        public void RemoveAgent(string agentId)
        {
            AgentIds = AgentIds.Except([agentId]).ToArray();
        }
    }
}
