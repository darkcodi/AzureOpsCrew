namespace AzureOpsCrew.Domain.Chats;

public class RawLlmHttpCall
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public Guid ThreadId { get; set; }
    public Guid RunId { get; set; }
    public string HttpRequest { get; set; } = string.Empty;
    public string HttpResponse { get; set; } = string.Empty;
}
