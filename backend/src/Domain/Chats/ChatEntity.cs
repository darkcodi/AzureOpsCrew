#pragma warning disable CS8618

namespace AzureOpsCrew.Domain.Chats;

public sealed class ChatEntity
{
    public ChatEntity() { }

    public ChatEntity(Guid id, string title)
    {
        Id = id;
        Title = title;
    }

    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid[] ParticipantIds { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DateModified { get; set; }

    public void UpdateTitle(string title)
    {
        Title = title;
        DateModified = DateTime.UtcNow;
    }

    public void AddParticipant(Guid id)
    {
        ParticipantIds = ParticipantIds.Concat([id]).ToArray();
        DateModified = DateTime.UtcNow;
    }

    public void RemoveParticipant(Guid id)
    {
        ParticipantIds = ParticipantIds.Except([id]).ToArray();
        DateModified = DateTime.UtcNow;
    }
}
