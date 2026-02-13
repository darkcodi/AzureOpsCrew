namespace AzureOpsCrew.Domain.Chats
{
    public class Message
    {
        private Message()
        {
        }

        public Message(Guid id, MessageSender sender, string text)
        {
            Id = id;
            Sender = sender;
            Text = text;
        }

        public Guid Id { get; private set; }
        
        public MessageSender Sender { get; private set; }

        //DRAFT public ProcessedResult, ProcessedByInfo - record to fix processed status by each agent in case parallel agents handling
        
        public string Text { get; private set; }
        
        public MessageStatus Status { get; set; } = MessageStatus.Sent;

        public DateTime DateCreated { get; private set; } = DateTime.UtcNow;

    }
}
