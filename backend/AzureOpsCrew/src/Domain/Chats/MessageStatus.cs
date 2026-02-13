namespace AzureOpsCrew.Domain.Chats
{
    public enum MessageStatus //Draft statuses
    {
        Sent = 0,
        
        Processing = 1000,
        
        ProcessedByAgent = 2001,
        ProcessedByClient = 2002,
        
        Failed = 5000
    }
}
