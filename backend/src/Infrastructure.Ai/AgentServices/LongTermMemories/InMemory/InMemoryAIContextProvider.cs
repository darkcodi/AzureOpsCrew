using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Reflection;
using System.Text.Json;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices.LongTermMemories.InMemory
{
    public class InMemoryAIContextProvider : AIContextProvider
    {
        protected override ValueTask<AIContext> InvokingCoreAsync(InvokingContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class InMemoryFactsContextProvider : AIContextProvider
    {
        private readonly List<AITool> _tools;
        private readonly string _memoryInstructions;

        public InMemoryFactsContextProvider(
            string agentId,
            InMemoryFactsStore store,
            JsonSerializerOptions? jsonSerializerOptions = null)
        {
            if (string.IsNullOrWhiteSpace(agentId))
                throw new ArgumentException("agentId is required", nameof(agentId));

            var toolset = new FactsMemoryTools(store, agentId);

            _tools = BuildTools(toolset, jsonSerializerOptions);

            _memoryInstructions = MemoryHint;
        }

        protected override ValueTask<AIContext> InvokingCoreAsync(
            AIContextProvider.InvokingContext context,
            CancellationToken cancellationToken = default)
        {
            // Важливо: не інжектимо всі факти кожен раз (щоб не з’їдати контекст),
            // натомість агент має викликати memory_search_facts при потребі.
            return new ValueTask<AIContext>(new AIContext
            {
                Instructions = _memoryInstructions,
                Tools = _tools
            });
        }

        private static List<AITool> BuildTools(FactsMemoryTools toolset, JsonSerializerOptions? serializerOptions)
        {
            var t = typeof(FactsMemoryTools);

            return new List<AITool>
            {
            CreateTool(t.GetMethod(nameof(FactsMemoryTools.AddFact))!, toolset,
                name: "memory_add_fact",
                description: "Add a durable fact to long-term memory.",
                serializerOptions),

            CreateTool(t.GetMethod(nameof(FactsMemoryTools.UpdateFact))!, toolset,
                name: "memory_update_fact",
                description: "Update an existing fact by id.",
                serializerOptions),

            CreateTool(t.GetMethod(nameof(FactsMemoryTools.DeleteFact))!, toolset,
                name: "memory_delete_fact",
                description: "Delete a fact by id.",
                serializerOptions),

            CreateTool(t.GetMethod(nameof(FactsMemoryTools.SearchFacts))!, toolset,
                name: "memory_search_facts",
                description: "Search stored facts by query.",
                serializerOptions),

            // optional:
            CreateTool(t.GetMethod(nameof(FactsMemoryTools.ListFacts))!, toolset,
                name: "memory_list_facts",
                description: "List recent facts (debug).",
                serializerOptions),
        };
        }

        private static AITool CreateTool(MethodInfo method, object target, string name, string description, JsonSerializerOptions? serializerOptions)
        {
            var options = new AIFunctionFactoryOptions
            {
                Name = name,
                Description = description,
                SerializerOptions = serializerOptions
            };

            return AIFunctionFactory.Create(method, target, options);
        }

        private const string MemoryHint = """
# Long-term memory: Facts (scoped by Agent.Id)
You have access to long-term memory tools:

- memory_add_fact(text, category?) -> saves a durable fact and returns it with an id
- memory_update_fact(factId, text, category?) -> updates a fact by id
- memory_delete_fact(factId) -> deletes a fact by id
- memory_search_facts(query, limit?) -> searches facts and returns matches
- memory_list_facts(limit?) -> lists recent facts (debug)

## When to STORE (call memory_add_fact)
Store only durable, future-useful information such as:
- user preferences (tone, language, style, UX/UI choices)
- user profile facts the user explicitly wants remembered (name, role, timezone)
- long-lived goals, constraints, requirements, non-negotiables
- stable project settings / environment configuration / decisions that should persist

## When NOT to store
Do NOT store:
- secrets (passwords, API keys, auth tokens, private keys)
- highly sensitive personal data (full address, payment card numbers, etc.)
- one-off ephemeral details unless the user explicitly says it should be remembered

## How to STORE
- Keep each fact short, atomic, and self-contained.
- Avoid pronouns; include the subject: "User prefers …"
- If new info contradicts an old fact: search first, then update the existing fact instead of creating duplicates.

## When to RETRIEVE (call memory_search_facts)
If the user asks to recall/remember, references the past ("as I said before"), or you need a known preference/setting to answer well:
- Use memory_search_facts first.
- Do not guess or claim you remember something without searching.

## Output
When you use retrieved facts, incorporate them naturally in your answer.
If relevant, you may mention you used stored memory (without exposing internal tool JSON).
""";
    }
}
