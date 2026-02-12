using System;
using System.Collections.Generic;
using System.Text;

namespace AzureOpsCrew.Domain.Agents
{
    public class Agent(Guid guid, int clientId, AgentInfo info, Provider provider, string providerAgentId)
    {
        public Guid Guid { get; private set; } = guid;

        public string ProviderAgentId { get; private set; } = providerAgentId;

        public int ClientId { get; private set; } = clientId;
        
        public AgentInfo Info { get; private set; } = info;

        public Provider Provider { get; private set; } = provider;

        public AgentTool[] AvaliableTools { get; set; } = [];

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    }
}
