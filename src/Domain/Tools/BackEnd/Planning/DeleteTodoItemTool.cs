using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Utils;

namespace AzureOpsCrew.Domain.Tools.BackEnd.Planning;

public class DeleteTodoItemTool : ITool
{
    public ToolDeclaration GetDeclaration()
    {
        return new ToolDeclaration
        {
            Name = "deleteTodoItem",
            Description = "Deletes a to-do item from your to-do list. Use this tool when you have completed a task or no longer need it in your to-do list.",
            JsonSchema = JsonUtils.Schema("""
                                          {
                                            "type": "object",
                                            "properties": {
                                              "id": { "type": "string", "description": "The unique identifier of the to-do item to delete" }
                                            },
                                            "required": ["id"],
                                            "additionalProperties": false
                                          }
                                          """).ToString(),
            ReturnJsonSchema = JsonUtils.Schema("""
                                                {
                                                  "type": "object",
                                                  "properties": {
                                                    "success": { "type": "boolean", "description": "Indicates whether the to-do item was successfully deleted" }
                                                  },
                                                  "required": ["success"],
                                                  "additionalProperties": false
                                                }
                                                """).ToString(),
            ToolType = ToolType.BackEnd,
        };
    }

    public Task<ToolCallResult> ExecuteAsync(Agent agent, string callId, IDictionary<string, object?>? arguments)
    {
        if (!ToDoStorage.ToDoItems.TryGetValue(agent.Id, out var toDoItems))
        {
            toDoItems = new List<ToDoItem>();
            ToDoStorage.ToDoItems[agent.Id] = toDoItems;
        }

        // validate arguments
        if (arguments == null || !arguments.ContainsKey("id") || string.IsNullOrEmpty(arguments["id"]?.ToString()))
        {
            return Task.FromResult(new ToolCallResult(callId, new { ErrorMessage = "id param is missing or empty" }, true));
        }

        var id = arguments["id"]?.ToString() ?? string.Empty;

        var itemToRemove = toDoItems.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.InvariantCultureIgnoreCase));
        if (itemToRemove != null)
        {
            toDoItems.Remove(itemToRemove);
            return Task.FromResult(new ToolCallResult(callId, new { wasFound = true, wasRemoved = true }, false));
        }
        else
        {
            return Task.FromResult(new ToolCallResult(callId, new { wasFound = false, wasRemoved = false }, false));
        }
    }
}
