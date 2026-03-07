using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Utils;

namespace AzureOpsCrew.Domain.Tools.BackEnd.Planning;

public class CreateTodoItemTool : IBackendTool
{
    public ToolDeclaration GetDeclaration()
    {
        return new ToolDeclaration
        {
            Name = "createTodoItem",
            Description = "Creates a new to-do item in the your to-do list. Use this tool to keep track of tasks you need to complete. The tool accepts a title and an optional description for the to-do item.",
            JsonSchema = JsonUtils.Schema("""
                                          {
                                            "type": "object",
                                            "properties": {
                                              "title": { "type": "string", "description": "The title of the to-do item" },
                                              "description": { "type": "string", "description": "An optional description providing more details about the to-do item" }
                                            },
                                            "required": ["title"],
                                            "additionalProperties": false
                                          }
                                          """).ToString(),
            ReturnJsonSchema = JsonUtils.Schema("""
                                                {
                                                  "type": "object",
                                                  "properties": {
                                                    "todoItemId": { "type": "string", "description": "The unique identifier of the created to-do item" }
                                                  },
                                                  "required": ["todoItemId"],
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
        if (arguments == null || !arguments.ContainsKey("title") || string.IsNullOrEmpty(arguments["title"]?.ToString()))
        {
            return Task.FromResult(new ToolCallResult(callId, new { ErrorMessage = "title param is missing or empty" }, true));
        }

        var title = arguments["title"]?.ToString() ?? string.Empty;
        var description = arguments.TryGetValue("description", out var argument) ? argument?.ToString() : null;

        var newItem = new ToDoItem
        {
            Title = title,
            Description = description,
            IsCompleted = false,
            CompletionSummary = null,
        };
        toDoItems.Add(newItem);

        return Task.FromResult(new ToolCallResult(callId, new { todoItemId = newItem.Id }, false));
    }
}
