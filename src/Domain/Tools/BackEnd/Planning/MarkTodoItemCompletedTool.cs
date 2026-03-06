using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Utils;

namespace AzureOpsCrew.Domain.Tools.BackEnd.Planning;

public class MarkTodoItemCompletedTool : ITool
{
    public ToolDeclaration GetDeclaration()
    {
        return new ToolDeclaration
        {
            Name = "markTodoItemCompleted",
            Description = "Marks a to-do item as completed. Use this tool when you have finished a task and want to update its status in your to-do list.",
            JsonSchema = JsonUtils.Schema("""
                                          {
                                            "type": "object",
                                            "properties": {
                                              "id": { "type": "string", "description": "The unique identifier of the to-do item to mark as completed" }
                                            },
                                            "required": ["id"],
                                            "additionalProperties": false
                                          }
                                          """).ToString(),
            ReturnJsonSchema = JsonUtils.Schema("""
                                                {
                                                  "type": "object",
                                                  "properties": {
                                                    "success": { "type": "boolean", "description": "Indicates whether the operation was successful" }
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

        var itemToMark = toDoItems.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.InvariantCultureIgnoreCase));
        if (itemToMark != null)
        {
            itemToMark.IsCompleted = true;
            return Task.FromResult(new ToolCallResult(callId, new { success = true }, false));
        }
        else
        {
            return Task.FromResult(new ToolCallResult(callId, new { success = false, ErrorMessage = "To-do item with the specified id was not found" }, false));
        }
    }
}
