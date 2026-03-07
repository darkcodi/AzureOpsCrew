namespace AzureOpsCrew.Domain.Tools.BackEnd.Planning;

public class ToDoItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsCompleted { get; set; } = false;
    public string? CompletionSummary { get; set; }
}
