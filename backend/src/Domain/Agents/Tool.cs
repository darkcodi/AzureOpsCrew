namespace AzureOpsCrew.Domain.Agents
{
    /// <summary>
    /// Represents information about a tool available to an agent
    /// </summary>
    public class AgentTool
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
