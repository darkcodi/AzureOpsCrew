namespace AzureOpsCrew.Api.Endpoints.Dtos.Chats;

public record SendChatMessageBodyDto
{
    public int ClientId { get; set; }

    public string Text { get; set; } = string.Empty;
}
