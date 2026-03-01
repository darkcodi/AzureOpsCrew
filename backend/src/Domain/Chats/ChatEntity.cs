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
    public Guid[] ParticipantUserIds { get; private set; } = [];
    public Guid[] ParticipantAgentIds { get; private set; } = [];
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? DateModified { get; private set; }

    public void UpdateTitle(string title)
    {
        Title = title;
        DateModified = DateTime.UtcNow;
    }

    public void AddParticipantUser(Guid userId)
    {
        ParticipantUserIds = ParticipantUserIds.Concat([userId]).ToArray();
        DateModified = DateTime.UtcNow;
    }

    public void RemoveParticipantUser(Guid userId)
    {
        ParticipantUserIds = ParticipantUserIds.Except([userId]).ToArray();
        DateModified = DateTime.UtcNow;
    }

    public void AddParticipantAgent(Guid agentId)
    {
        ParticipantAgentIds = ParticipantAgentIds.Concat([agentId]).ToArray();
        DateModified = DateTime.UtcNow;
    }

    public void RemoveParticipantAgent(Guid agentId)
    {
        ParticipantAgentIds = ParticipantAgentIds.Except([agentId]).ToArray();
        DateModified = DateTime.UtcNow;
    }
}
