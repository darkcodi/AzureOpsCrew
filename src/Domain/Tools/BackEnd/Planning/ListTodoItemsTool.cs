using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Utils;

namespace AzureOpsCrew.Domain.Tools.BackEnd.Planning;

public class ListTodoItemsTool : ITool
{
    public ToolDeclaration GetDeclaration()
    {
        return new ToolDeclaration
        {
            Name = "listTodoItems",
            Description = "Lists all to-do items in your to-do list. Use this tool to see the tasks you have created and need to complete.",
            JsonSchema = JsonUtils.Schema("""
                                          {
                                            "type": "object",
                                            "properties": {},
                                            "additionalProperties": false
                                          }
                                          """).ToString(),
            ReturnJsonSchema = JsonUtils.Schema("""
                                                {
                                                  "type": "object",
                                                  "properties": {
                                                    "todoItems": {
                                                      "type": "array",
                                                      "items": {
                                                        "type": "object",
                                                        "properties": {
                                                          "id": { "type": "string", "description": "The unique identifier of the to-do item" },
                                                          "title": { "type": "string", "description": "The title of the to-do item" },
                                                          "description": { "type": ["string", "null"], "description": "An optional description providing more details about the to-do item" }
                                                        },
                                                        "required": ["id", "title"],
                                                        "additionalProperties": false
                                                      },
                                                      "description": "A list of all to-do items in your to-do list"
                                                    }
                                                  },
                                                  "required": ["todoItems"],
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

        var result = new
        {
            todoItems = toDoItems.Select(item => new
            {
                id = item.Id,
                title = item.Title,
                description = item.Description,
                isCompleted = item.IsCompleted,
            }).ToList()
        };

        return Task.FromResult(new ToolCallResult(callId, result, false));
    }
}
