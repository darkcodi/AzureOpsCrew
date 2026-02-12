namespace AzureOpsCrew.Domain.Agents
{
    public record AgentInfo(string Name, string Prompt, string Model)
    {
        public string Name { get; private set; } = Name;

        public string Prompt { get; private set; } = Prompt;
        
        public string Model { get; private set; } = Model;

        public string? Description { get; set; }
    }
}
