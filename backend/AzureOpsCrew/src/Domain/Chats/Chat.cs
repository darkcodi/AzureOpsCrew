namespace AzureOpsCrew.Domain.Chats
{
    public class Chat
    {
        private Chat()
        {
        }

        public Chat(Guid id, int clientId)
        {
            Id = id;
            ClientId = clientId;
        }

        public Guid Id { get; set; }

        public int ClientId { get; set; }

        public int[] AgentIds { get; set; } = [];

        public List<Message> Messages { get; private set; } = []; //Change on IEnumerable<>(with shodow list property) to avoid uncontrol modification. Posible EF Core issues.

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;


        public void AddMessage(string text, MessageSender sender)
        {
            Messages.Add(new Message(Guid.NewGuid(), sender, text));
        }
    }
}
