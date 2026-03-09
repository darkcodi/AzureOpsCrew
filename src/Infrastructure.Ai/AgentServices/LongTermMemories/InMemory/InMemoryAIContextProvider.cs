using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Reflection;
using System.Text.Json;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices.LongTermMemories.InMemory
{
    public sealed class InMemoryFactsContextProvider : AIContextProvider
    {
        private readonly List<AITool> _tools;
        private readonly string _memoryInstructions;

        public InMemoryFactsContextProvider(
            Guid agentId,
            InMemoryFactsStore store,
            JsonSerializerOptions? jsonSerializerOptions = null)
        {
            if (store is null)
                throw new ArgumentNullException(nameof(store));

            var toolset = new FactsMemoryTools(store, agentId);

            _tools = BuildTools(toolset, jsonSerializerOptions).ToList();
            _memoryInstructions = MemoryHint;
        }

        protected override ValueTask<AIContext> InvokingCoreAsync(
            InvokingContext context,
            CancellationToken cancellationToken = default)
        {
            var aiContext = new AIContext
            {
                Instructions = _memoryInstructions,
                Tools = _tools
                // Messages = null;
            };

            return new ValueTask<AIContext>(aiContext);
        }

        private static IEnumerable<AITool> BuildTools(FactsMemoryTools toolset, JsonSerializerOptions? serializerOptions)
        {
            var t = typeof(FactsMemoryTools);

            MethodInfo GetRequired(string name) =>
                t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public)
                ?? throw new MissingMethodException(t.FullName, name);

            var tools = new List<AITool>
        {
            CreateTool(GetRequired(nameof(FactsMemoryTools.AddFact)), toolset,
                name: "memory_add_fact",
                description: "Add a durable fact to long-term memory.",
                serializerOptions),

            CreateTool(GetRequired(nameof(FactsMemoryTools.UpdateFact)), toolset,
                name: "memory_update_fact",
                description: "Update an existing fact by id.",
                serializerOptions),

            CreateTool(GetRequired(nameof(FactsMemoryTools.DeleteFact)), toolset,
                name: "memory_delete_fact",
                description: "Delete a fact by id.",
                serializerOptions),

            CreateTool(GetRequired(nameof(FactsMemoryTools.SearchFacts)), toolset,
                name: "memory_search_facts",
                description: "Search stored facts by query.",
                serializerOptions),
        };

            // optional tool (не ламає збірку, якщо ти прибрав ListFacts)
            var listFacts = t.GetMethod("ListFacts", BindingFlags.Instance | BindingFlags.Public);
            if (listFacts is not null)
            {
                tools.Add(CreateTool(listFacts, toolset,
                    name: "memory_list_facts",
                    description: "List recent facts (debug).",
                    serializerOptions));
            }

            return tools;
        }

        private static AITool CreateTool(
            MethodInfo method,
            object target,
            string name,
            string description,
            JsonSerializerOptions? serializerOptions)
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
# Long-term memory (agent-scoped)
Each agent has its own isolated fact store. Facts persist across sessions but are NOT shared between agents.

## Available tools
- memory_add_fact(text, category?) — save a new fact
- memory_update_fact(factId, text, category?) — update existing fact by id
- memory_delete_fact(factId) — delete fact by id
- memory_search_facts(query, limit?) — search facts by relevance
- memory_list_facts(limit?) — list recent facts (debug)

## When to RETRIEVE
IMPORTANT: Do not claim you remember something unless you searched first.

Search proactively when:
- User references past context ("as I said", "do you remember", "last time")
- A preference, setting, or constraint could change your answer
- You are about to assume something that might have been stated before

## Search query syntax
memory_search_facts supports these operators (combine freely):

| Operator | Syntax | Example |
|---|---|---|
| Exact phrase (MUST match) | `"phrase"` | `"error budget"`, `"pipeline yaml"` |
| Category filter (MUST match) | `cat:value` or `#value` | `cat:preference`, `#project` |
| Exclusion (MUST NOT match) | `-term` | `-legacy`, `-deprecated` |
| ID lookup (strongest signal) | paste fact id | `a1b2c3d4` |

Combined: memory_search_facts("cat:preference -deprecated", 8)

## Retrieval strategy
1. Search with user’s original phrasing: `memory_search_facts("<user phrase>", 8)`
2. If weak — add filters: `cat:preference`, `"exact phrase"`, `-exclusion`
3. If still weak — compact keywords (2-6 words), synonyms, alternative phrasings, domain entities
Keep to 2-4 searches max per turn.

## When to STORE
Store durable, future-useful facts:
- User preferences (language, tone, formatting, verbosity)
- Stable profile facts meant to be remembered (role, timezone, name)
- Long-lived goals, constraints, requirements, "always/never" rules
- Stable project decisions, conventions, environment settings

If unsure whether it’s durable — ask the user or don’t store.

## How to STORE
IMPORTANT: Format facts for optimal search recall.
- One atomic idea per fact
- Include 2-3 compact rephrasings/aliases separated by `;`
- Use stable keywords, not fluffy prose
- ALWAYS end with: `tags: tag1, tag2, tag3`
  - Short, lowercase, stable keywords
  - Multilingual variants when relevant
  - Domain keywords (project/tool/feature names)
  - Reflect the category in tags (e.g., category=preference => tag "preference")

<example>
User prefers concise answers; short responses; minimal verbosity. tags: preference, style, concise, verbosity
</example>

<example>
Project uses .NET 10; target framework net10.0; dotnet 10. tags: project, dotnet, net10, framework
</example>

## Update vs Add
If new info conflicts with existing memory — search first, then use memory_update_fact instead of creating duplicates.

## Do NOT store
- Secrets (passwords, API keys, tokens, private keys)
- Sensitive personal data (full address, payment cards)
- Ephemeral one-off details (unless user explicitly asks to remember)

## Output
Use retrieved facts naturally in your response. Do not expose internal tool JSON.
""";
    }
}
