namespace AzureOpsCrew.Domain.Chats
{
    public class Chat
    {
        private Chat()
        {
        }

        public Chat(Guid id, int clientId, string name)
        {
            Id = id;
            ClientId = clientId;
            Name = name;
        }

        public Guid Id { get; set; }

        public int ClientId { get; set; }
        
        public string Name { get; set; }

        public string? Description { get; set; }


        public Guid[] AgentIds { get; set; } = [];

        public List<Message> Messages { get; private set; } = []; //Change on IEnumerable<>(with shodow list property) to avoid uncontrol modification. Posible EF Core issues.

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;


        public void AddMessage(string text, MessageSender sender)
        {
            Messages.Add(new Message(Guid.NewGuid(), sender, text));
        }
    }
}
