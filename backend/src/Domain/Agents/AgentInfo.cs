namespace AzureOpsCrew.Domain.Agents
{
    public record AgentInfo(string Name, string Prompt, string Model)
    {
        public string? Description { get; set; }

        public AgentTool[] AvailableTools { get; set; } = [];
    }
}
