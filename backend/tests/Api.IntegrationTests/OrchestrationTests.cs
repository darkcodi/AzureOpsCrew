using AzureOpsCrew.Api.Orchestration;
using AzureOpsCrew.Api.Settings;
using Microsoft.Extensions.AI;
using Xunit;

namespace AzureOpsCrew.Api.IntegrationTests;

/// <summary>
/// Tests for multi-agent orchestration features.
/// Addresses: structured delegation, tool enforcement, direct addressing.
/// </summary>
public class OrchestrationTests
{
    #region DelegationModels Tests

    [Fact]
    public void DelegatedTask_RequiresTools_DefaultsToTrue()
    {
        var task = new DelegatedTask
        {
            Assignee = "DevOps",
            Intent = TaskIntents.Diagnostic,
            Goal = "Check container health"
        };

        Assert.True(task.RequiresTools);
    }

    [Fact]
    public void DelegatedTask_CanSpecifyRequiredTools()
    {
        var task = new DelegatedTask
        {
            Assignee = "DevOps",
            Intent = TaskIntents.Inventory,
            Goal = "List all Azure resources",
            RequiredTools = new List<string> { "azure_list_resources", "arg_query" }
        };

        Assert.Equal(2, task.RequiredTools.Count);
        Assert.Contains("azure_list_resources", task.RequiredTools);
    }

    [Fact]
    public void TaskIntents_HasAllExpectedValues()
    {
        Assert.Equal("inventory", TaskIntents.Inventory);
        Assert.Equal("diagnostic", TaskIntents.Diagnostic);
        Assert.Equal("remediation", TaskIntents.Remediation);
        Assert.Equal("verification", TaskIntents.Verification);
        Assert.Equal("code_analysis", TaskIntents.CodeAnalysis);
        Assert.Equal("code_fix", TaskIntents.CodeFix);
        Assert.Equal("generic", TaskIntents.Generic);
    }

    [Fact]
    public void DelegatedTaskStatus_HasAllStatuses()
    {
        var values = Enum.GetValues<DelegatedTaskStatus>();
        
        Assert.Contains(DelegatedTaskStatus.Queued, values);
        Assert.Contains(DelegatedTaskStatus.Running, values);
        Assert.Contains(DelegatedTaskStatus.Completed, values);
        Assert.Contains(DelegatedTaskStatus.Failed, values);
        Assert.Contains(DelegatedTaskStatus.RejectedNoTools, values);
    }

    #endregion

    #region RunContext Delegation Queue Tests

    [Fact]
    public void RunContext_QueueDelegatedTask_EnqueuesTask()
    {
        // Arrange
        var context = new RunContext("run-1", "thread-1", Guid.NewGuid(), "test request");
        var task = new DelegatedTask
        {
            Assignee = "DevOps",
            Intent = TaskIntents.Diagnostic,
            Goal = "Check health"
        };

        // Act
        context.QueueDelegatedTask(task, "task-1");

        // Assert
        Assert.True(context.HasDelegatedTasks());
    }

    [Fact]
    public void RunContext_DequeueNextTask_ReturnsTask()
    {
        // Arrange
        var context = new RunContext("run-1", "thread-1", Guid.NewGuid(), "test request");
        var task = new DelegatedTask
        {
            Assignee = "DevOps",
            Intent = TaskIntents.Inventory,
            Goal = "List resources"
        };
        context.QueueDelegatedTask(task, "task-1");

        // Act
        var dequeued = context.DequeueNextTask();

        // Assert
        Assert.NotNull(dequeued);
        Assert.Equal("DevOps", dequeued.Value.Task.Assignee);
        Assert.Equal("task-1", dequeued.Value.TaskId);
    }

    [Fact]
    public void RunContext_DequeueNextTask_SetsCurrentTask()
    {
        // Arrange
        var context = new RunContext("run-1", "thread-1", Guid.NewGuid(), "test request");
        var task = new DelegatedTask
        {
            Assignee = "DevOps",
            Intent = TaskIntents.Diagnostic,
            Goal = "Diagnose issue"
        };
        context.QueueDelegatedTask(task, "task-1");

        // Act
        context.DequeueNextTask();

        // Assert
        Assert.NotNull(context.CurrentTask);
        Assert.Equal("task-1", context.CurrentTask.Value.TaskId);
    }

    [Fact]
    public void RunContext_DequeueNextTask_EmptyQueue_ReturnsNull()
    {
        // Arrange
        var context = new RunContext("run-1", "thread-1", Guid.NewGuid(), "test request");

        // Act
        var result = context.DequeueNextTask();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void RunContext_CompleteCurrentTask_ClearsCurrentTask()
    {
        // Arrange
        var context = new RunContext("run-1", "thread-1", Guid.NewGuid(), "test request");
        var task = new DelegatedTask { Assignee = "DevOps", Goal = "Test" };
        context.QueueDelegatedTask(task, "task-1");
        context.DequeueNextTask();

        // Act
        context.CompleteCurrentTask(true, "Done successfully");

        // Assert
        Assert.Null(context.CurrentTask);
    }

    [Fact]
    public void RunContext_RecordMissingToolRetry_IncrementsCounters()
    {
        // Arrange
        var context = new RunContext("run-1", "thread-1", Guid.NewGuid(), "test request");
        var task = new DelegatedTask 
        { 
            Assignee = "DevOps", 
            Goal = "Test", 
            RequiresTools = true 
        };
        context.QueueDelegatedTask(task, "task-1");
        context.DequeueNextTask();

        // Act
        context.RecordMissingToolRetry();

        // Assert
        Assert.Equal(1, context.CurrentTaskMissingToolRetries);
        Assert.Equal(1, context.MissingToolRetryCount);
    }

    [Fact]
    public void RunContext_MultipleRetries_AccumulatesCorrectly()
    {
        // Arrange
        var context = new RunContext("run-1", "thread-1", Guid.NewGuid(), "test request");
        var task = new DelegatedTask { Assignee = "DevOps", Goal = "Test", RequiresTools = true };
        context.QueueDelegatedTask(task, "task-1");
        context.DequeueNextTask();

        // Act
        context.RecordMissingToolRetry();
        context.RecordMissingToolRetry();

        // Assert
        Assert.Equal(2, context.CurrentTaskMissingToolRetries);
        Assert.Equal(2, context.MissingToolRetryCount);
    }

    #endregion

    #region Direct Addressing Tests

    [Fact]
    public void RunContext_SetDirectAddress_StoresAddress()
    {
        // Arrange
        var context = new RunContext("run-1", "thread-1", Guid.NewGuid(), "test request");
        var address = new DirectAddressing
        {
            IsDirect = true,
            AddressedTo = "DevOps",
            OriginalMessage = "@DevOps check health"
        };

        // Act
        context.SetDirectAddress(address);

        // Assert
        Assert.NotNull(context.DirectAddress);
        Assert.True(context.DirectAddress.IsDirect);
        Assert.Equal("DevOps", context.DirectAddress.AddressedTo);
    }

    [Fact]
    public void DirectAddressing_NotDirect_DefaultState()
    {
        var address = new DirectAddressing();
        
        Assert.False(address.IsDirect);
        Assert.Null(address.AddressedTo);
        Assert.Null(address.OriginalMessage);
    }

    #endregion

    #region Metrics Tests

    [Fact]
    public void RunContext_RecordInventorySource_IncrementsCount()
    {
        // Arrange
        var context = new RunContext("run-1", "thread-1", Guid.NewGuid(), "test request");

        // Act
        context.RecordInventorySource();
        context.RecordInventorySource();

        // Assert
        Assert.Equal(2, context.InventorySourceCount);
    }

    [Fact]
    public void RunContext_RecordArtifactSaved_IncrementsCount()
    {
        // Arrange
        var context = new RunContext("run-1", "thread-1", Guid.NewGuid(), "test request");

        // Act
        context.RecordArtifactSaved();

        // Assert
        Assert.Equal(1, context.ArtifactsSaved);
    }

    [Fact]
    public void RunContext_RecordTruncation_IncrementsCount()
    {
        // Arrange
        var context = new RunContext("run-1", "thread-1", Guid.NewGuid(), "test request");

        // Act
        context.RecordTruncation();
        context.RecordTruncation();
        context.RecordTruncation();

        // Assert
        Assert.Equal(3, context.TruncationCount);
    }

    [Fact]
    public void RunContext_ToSummary_IncludesMetrics()
    {
        // Arrange
        var context = new RunContext("run-1", "thread-1", Guid.NewGuid(), "test request");
        context.RecordInventorySource();
        context.RecordArtifactSaved();
        context.RecordMissingToolRetry();

        // Act
        var summary = context.ToSummary();

        // Assert
        Assert.Contains("InventorySources=1", summary);
        Assert.Contains("ArtifactsSaved=1", summary);
        Assert.Contains("MissingToolRetries=1", summary);
    }

    #endregion

    #region OrchestrationSettings Feature Flag Tests

    [Fact]
    public void OrchestrationSettings_DefaultFeatureFlagsEnabled()
    {
        var settings = new OrchestrationSettings();

        Assert.True(settings.EnableStructuredDelegation);
        Assert.True(settings.EnableDirectAddressing);
        Assert.True(settings.EnableCompositeInventoryTool);
        Assert.True(settings.EnableArtifactFirst);
        Assert.True(settings.EnableToolEnforcement);
    }

    [Fact]
    public void OrchestrationSettings_DefaultRetryLimits()
    {
        var settings = new OrchestrationSettings();

        Assert.Equal(2, settings.MaxMissingToolRetries);
        Assert.Equal(6000, settings.ToolInlineThresholdChars);
        Assert.Equal(50, settings.MaxInventoryPages);
    }

    #endregion

    #region DelegationModels Serialization Tests

    [Fact]
    public void DelegationRequest_CanSerializeAndDeserialize()
    {
        // Arrange
        var request = new DelegationRequest
        {
            Tasks = new List<DelegatedTask>
            {
                new DelegatedTask
                {
                    Assignee = "DevOps",
                    Intent = TaskIntents.Diagnostic,
                    Goal = "Check container health",
                    RequiresTools = true,
                    RequiredTools = new List<string> { "container_get_health" },
                    DefinitionOfDone = "Provide health status with metrics"
                }
            }
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(request);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<DelegationRequest>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Single(deserialized.Tasks);
        Assert.Equal("DevOps", deserialized.Tasks[0].Assignee);
        Assert.Equal(TaskIntents.Diagnostic, deserialized.Tasks[0].Intent);
        Assert.True(deserialized.Tasks[0].RequiresTools);
    }

    [Fact]
    public void DelegatedTaskResult_CanTrackStatus()
    {
        var result = new DelegatedTaskResult
        {
            TaskId = "task-1",
            Assignee = "DevOps",
            Intent = TaskIntents.Inventory,
            Status = DelegatedTaskStatus.Running,
            Summary = "Processing inventory request"
        };

        Assert.Equal("task-1", result.TaskId);
        Assert.Equal(DelegatedTaskStatus.Running, result.Status);
    }

    [Fact]
    public void DelegatedTaskResult_CanRecordFailure()
    {
        var result = new DelegatedTaskResult
        {
            TaskId = "task-1",
            Assignee = "DevOps",
            Intent = TaskIntents.Remediation,
            Status = DelegatedTaskStatus.Failed,
            ErrorMessage = "Permission denied",
            RetryCount = 2
        };

        Assert.Equal(DelegatedTaskStatus.Failed, result.Status);
        Assert.Equal("Permission denied", result.ErrorMessage);
        Assert.Equal(2, result.RetryCount);
    }

    #endregion

    #region InventoryResult Tests

    [Fact]
    public void InventoryResult_CanTrackMultipleSources()
    {
        var result = new InventoryResult
        {
            TotalResourceCount = 150,
            SourcesQueried = new List<string> { "Azure MCP", "Platform MCP ARG" },
            Truncated = false,
            Pages = 3
        };

        Assert.Equal(150, result.TotalResourceCount);
        Assert.Equal(2, result.SourcesQueried.Count);
        Assert.Contains("Azure MCP", result.SourcesQueried);
        Assert.Contains("Platform MCP ARG", result.SourcesQueried);
        Assert.False(result.Truncated);
    }

    [Fact]
    public void InventoryResult_CanIndicateTruncation()
    {
        var result = new InventoryResult
        {
            TotalResourceCount = 500,
            Truncated = true,
            ArtifactId = "artifact-123",
            Pages = 10
        };

        Assert.True(result.Truncated);
        Assert.Equal("artifact-123", result.ArtifactId);
    }

    #endregion
}
