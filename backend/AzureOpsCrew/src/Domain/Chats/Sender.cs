namespace AzureOpsCrew.Domain.Chats
{
    public record MessageSender
    {
        public MessageSenderType SenderType { get; private set; }

        public Guid? AgentId { get; private set; }

        private MessageSender()
        {
        }


        public MessageSender System() => new MessageSender { SenderType = MessageSenderType.System };
        public MessageSender Client() => new MessageSender { SenderType = MessageSenderType.System };

        public MessageSender Agent(Guid agentId) => new MessageSender { SenderType = MessageSenderType.System, AgentId = agentId };

    }

    public enum MessageSenderType
    {
        System = 0,
        Client = 1,
        Agent = 2,
    }
}
