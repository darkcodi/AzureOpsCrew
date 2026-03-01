namespace Chat.Endpoints.Dtos;

public record CreateChatDto(string Title, Guid[]? ParticipantIds);
