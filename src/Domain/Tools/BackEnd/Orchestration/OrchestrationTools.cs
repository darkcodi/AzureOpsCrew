using AzureOpsCrew.Domain.Utils;

namespace AzureOpsCrew.Domain.Tools.BackEnd.Orchestration;

/// <summary>
/// Static declarations for orchestration tools.
/// Execution is handled by OrchestrationToolExecutor in the Api layer.
/// </summary>
public static class OrchestrationTools
{
    public const string CreateTaskName = "createTask";
    public const string ListTasksName = "listTasks";
    public const string PostTaskProgressName = "postTaskProgress";
    public const string CompleteTaskName = "completeTask";
    public const string FailTaskName = "failTask";

    public static readonly IReadOnlyList<string> ManagerToolNames = [CreateTaskName, ListTasksName];
    public static readonly IReadOnlyList<string> WorkerToolNames = [PostTaskProgressName, CompleteTaskName, FailTaskName];

    public static List<ToolDeclaration> GetManagerTools() =>
    [
        new ToolDeclaration
        {
            Name = CreateTaskName,
            Description = "Creates a new task for a worker agent in the current channel. Use this to delegate specialist work to the appropriate agent.",
            JsonSchema = JsonUtils.Schema("""
            {
              "type": "object",
              "properties": {
                "agentUsername": { "type": "string", "description": "The username of the worker agent to assign the task to" },
                "title": { "type": "string", "description": "Short title of the task" },
                "description": { "type": "string", "description": "Detailed description of what the worker should do" },
                "announceInChat": { "type": "boolean", "description": "Whether to post a visible delegation message in the chat. Default: true" }
              },
              "required": ["agentUsername", "title", "description"],
              "additionalProperties": false
            }
            """).ToString(),
            ReturnJsonSchema = JsonUtils.Schema("""
            {
              "type": "object",
              "properties": {
                "taskId": { "type": "string", "description": "ID of the created task" },
                "assignedAgentUsername": { "type": "string" },
                "status": { "type": "string" }
              },
              "required": ["taskId", "status"],
              "additionalProperties": false
            }
            """).ToString(),
            ToolType = ToolType.Orchestration,
        },
        new ToolDeclaration
        {
            Name = ListTasksName,
            Description = "Lists orchestration tasks for the current channel. Use this to check task statuses and decide next steps or final synthesis.",
            JsonSchema = JsonUtils.Schema("""
            {
              "type": "object",
              "properties": {
                "status": { "type": "string", "description": "Filter by status: Pending, InProgress, Completed, Failed, or All. Default: All" }
              },
              "additionalProperties": false
            }
            """).ToString(),
            ReturnJsonSchema = JsonUtils.Schema("""
            {
              "type": "object",
              "properties": {
                "tasks": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "properties": {
                      "taskId": { "type": "string" },
                      "title": { "type": "string" },
                      "assignedAgent": { "type": "string" },
                      "status": { "type": "string" },
                      "progressSummary": { "type": "string" },
                      "resultSummary": { "type": "string" },
                      "failureReason": { "type": "string" }
                    }
                  }
                }
              },
              "required": ["tasks"],
              "additionalProperties": false
            }
            """).ToString(),
            ToolType = ToolType.Orchestration,
        },
    ];

    public static List<ToolDeclaration> GetWorkerTools() =>
    [
        new ToolDeclaration
        {
            Name = PostTaskProgressName,
            Description = "Posts a progress update for your current assigned task. Optionally mirrors the update as a visible message in the channel chat.",
            JsonSchema = JsonUtils.Schema("""
            {
              "type": "object",
              "properties": {
                "message": { "type": "string", "description": "Progress update message" },
                "mirrorToChat": { "type": "boolean", "description": "Whether to post this update as a visible message in the channel. Default: true" }
              },
              "required": ["message"],
              "additionalProperties": false
            }
            """).ToString(),
            ReturnJsonSchema = JsonUtils.Schema("""
            {
              "type": "object",
              "properties": {
                "status": { "type": "string" }
              },
              "required": ["status"],
              "additionalProperties": false
            }
            """).ToString(),
            ToolType = ToolType.Orchestration,
        },
        new ToolDeclaration
        {
            Name = CompleteTaskName,
            Description = "Completes your current assigned task with a result summary. The manager will be notified to review results.",
            JsonSchema = JsonUtils.Schema("""
            {
              "type": "object",
              "properties": {
                "result": { "type": "string", "description": "Summary of findings or results" },
                "mirrorToChat": { "type": "boolean", "description": "Whether to post the result as a visible message in the channel. Default: true" }
              },
              "required": ["result"],
              "additionalProperties": false
            }
            """).ToString(),
            ReturnJsonSchema = JsonUtils.Schema("""
            {
              "type": "object",
              "properties": {
                "status": { "type": "string" }
              },
              "required": ["status"],
              "additionalProperties": false
            }
            """).ToString(),
            ToolType = ToolType.Orchestration,
        },
        new ToolDeclaration
        {
            Name = FailTaskName,
            Description = "Fails your current assigned task with a reason. The manager will be notified to decide next steps.",
            JsonSchema = JsonUtils.Schema("""
            {
              "type": "object",
              "properties": {
                "reason": { "type": "string", "description": "Reason for failure or blocker description" },
                "mirrorToChat": { "type": "boolean", "description": "Whether to post the failure as a visible message in the channel. Default: true" }
              },
              "required": ["reason"],
              "additionalProperties": false
            }
            """).ToString(),
            ReturnJsonSchema = JsonUtils.Schema("""
            {
              "type": "object",
              "properties": {
                "status": { "type": "string" }
              },
              "required": ["status"],
              "additionalProperties": false
            }
            """).ToString(),
            ToolType = ToolType.Orchestration,
        },
    ];
}
