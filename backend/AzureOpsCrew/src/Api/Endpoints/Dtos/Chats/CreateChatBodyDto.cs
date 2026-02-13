namespace AzureOpsCrew.Api.Endpoints.Dtos.Chats;

public class CreateChatBodyDto
{
    public int ClientId { get; set; }

    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }

    public Guid[] AgentIds { get; set; } = [];
}
