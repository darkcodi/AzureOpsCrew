using AzureOpsCrew.Domain.ChatProcessings;
using AzureOpsCrew.Domain.Chats;

namespace AzureOpsCrew.Infrastructure.Ai.ChatProcessing
{
    public class StubChatProcessor : IChatProcessor
    {
        public async Task Process(Chat chat, CancellationToken cancellationToken)
        {
            chat.AddMessage("Stub chat processing message", MessageSender.Agent(chat.AgentIds.FirstOrDefault(Guid.Empty)));
        }
    }
}
