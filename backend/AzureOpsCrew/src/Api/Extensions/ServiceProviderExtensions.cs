using AzureOpsCrew.Infrastructure.Db;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Api.Extensions
{
    public static class ServiceProviderExtensions
    {
        public static async Task RunEnsureEFCoreCosmosDbCreated(this IServiceProvider provider)
        {
            // Measure the time taken to ensure the database is created
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("EnsureEFCoreCosmosDbCreated");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            logger.LogInformation("Starting database creation check...");
            using (var scope = provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AzureOpsCrewContext>();
                await context.Database.EnsureCreatedAsync();
            }
            stopwatch.Stop();
            logger.LogInformation("Database creation check completed in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
        }

        public static async Task TestAgents(this IServiceProvider provider)
        {
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("TestAgents");
            var chatClient = provider.GetRequiredService<IChatClient>();

            ChatClientAgent writer = new(chatClient,
                "You are a creative copywriter. Generate catchy slogans and marketing copy. Be concise and impactful.",
                "CopyWriter",
                "A creative copywriter agent");

            ChatClientAgent reviewer = new(chatClient,
                "You are a marketing reviewer. Evaluate slogans for clarity, impact, and brand alignment. " +
                "Provide constructive feedback or approval.",
                "Reviewer",
                "A marketing review agent");

            var agentsList = new List<ChatClientAgent> { writer, reviewer };
            logger.LogInformation("Starting TestAgents with {AgentCount} agents", agentsList.Count());

            // Build group chat with round-robin speaker selection
            // The manager factory receives the list of agents and returns a configured manager
            var workflow = AgentWorkflowBuilder
                .CreateGroupChatBuilderWith(agents =>
                    new RoundRobinGroupChatManager(agents)
                    {
                        MaximumIterationCount = 5  // Maximum number of turns
                    })
                .AddParticipants(agentsList)
                .Build();

            // Start the group chat
            var messages = new List<ChatMessage> {
                new(ChatRole.User, "Create a slogan for an eco-friendly electric vehicle.")
            };

            logger.LogInformation("Sending test message: {Message}", messages[0].Text);

            StreamingRun run = await InProcessExecution.StreamAsync(workflow, messages);
            await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

            string? currentExecutorId = null;
            bool hasLoggedForExecutor = false;
            var responseBuffer = new System.Text.StringBuilder();

            await foreach (WorkflowEvent evt in run.WatchStreamAsync().ConfigureAwait(false))
            {
                if (evt is AgentResponseUpdateEvent update)
                {
                    // Process streaming agent responses
                    AgentResponse response = update.AsResponse();
                    string executorId = update.ExecutorId ?? "unknown";

                    if (!string.Equals(currentExecutorId, executorId, StringComparison.Ordinal))
                    {
                        if (currentExecutorId is not null && hasLoggedForExecutor && responseBuffer.Length > 0)
                        {
                            logger.LogDebug("[{ExecutorId}]: {Response}", currentExecutorId, responseBuffer.ToString());
                            responseBuffer.Clear();
                        }

                        currentExecutorId = executorId;
                        hasLoggedForExecutor = false;
                    }

                    foreach (ChatMessage message in response.Messages)
                    {
                        if (string.IsNullOrEmpty(message.Text))
                        {
                            continue;
                        }

                        responseBuffer.Append(message.Text);
                        hasLoggedForExecutor = true;
                    }
                }
                else if (evt is WorkflowOutputEvent output)
                {
                    if (currentExecutorId is not null && hasLoggedForExecutor && responseBuffer.Length > 0)
                    {
                        logger.LogDebug("[{ExecutorId}]: {Response}", currentExecutorId, responseBuffer.ToString());
                    }

                    // Workflow completed
                    var conversationHistory = output.As<List<ChatMessage>>() ?? new List<ChatMessage>();
                    logger.LogInformation("=== Final Conversation ===");
                    foreach (var message in conversationHistory)
                    {
                        logger.LogInformation("{AuthorName}: {Text}", message.AuthorName, message.Text);
                    }
                    break;
                }
            }

            logger.LogInformation("TestAgents completed");
        }
    }
}
