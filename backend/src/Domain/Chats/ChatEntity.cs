#pragma warning disable CS8618

namespace AzureOpsCrew.Domain.Chats;

public sealed class ChatEntity
{
    private ChatEntity() { }

    public ChatEntity(Guid id, string title)
    {
        Id = id;
        Title = title;
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public Guid[] ParticipantIds { get; private set; } = [];
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? DateModified { get; private set; }

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
