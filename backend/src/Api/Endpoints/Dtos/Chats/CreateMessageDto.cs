namespace AzureOpsCrew.Api.Endpoints.Dtos.Chats;

public record CreateMessageDto
{
    public string AuthorName { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
}
