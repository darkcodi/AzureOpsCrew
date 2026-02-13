using AzureOpsCrew.Domain.Chats;

namespace AzureOpsCrew.Domain.ChatProcessings
{
    public interface IChatProcessor
    { 
        public Task Process(Chat chat, CancellationToken cancellationToken); //Finds unprocessed message(s) and start agent(s)
    }
}
