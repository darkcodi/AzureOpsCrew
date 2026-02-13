namespace AzureOpsCrew.Domain.Chats
{
    public record MessageSender
    {
        public MessageSenderType SenderType { get; private set; }

        public Guid? AgentId { get; private set; }

        private MessageSender()
        {
        }


        public static MessageSender System() => new MessageSender { SenderType = MessageSenderType.System };

        public static MessageSender Client() => new MessageSender { SenderType = MessageSenderType.Client };

        public static MessageSender Agent(Guid agentId) => new MessageSender { SenderType = MessageSenderType.Agent, AgentId = agentId };

    }

    public enum MessageSenderType
    {
        System = 0,
        Client = 1,
        Agent = 2,
    }
}
