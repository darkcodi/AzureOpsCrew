namespace AzureOpsCrew.Domain.Tools.BackEnd.Planning;

public static class ToDoStorage
{
    public static readonly IDictionary<Guid, List<ToDoItem>> ToDoItems = new Dictionary<Guid, List<ToDoItem>>();
}
