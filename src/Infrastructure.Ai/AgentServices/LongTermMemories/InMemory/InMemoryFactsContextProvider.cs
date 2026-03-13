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
                Tools = _tools.Any() ? null : _tools
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
Each agent has its own isolated fact store. Facts persist across all sessions but are NOT shared between agents.
Duplicate facts (same text + category) are automatically deduplicated.
Maximum 1000 facts per agent; oldest facts are evicted when the limit is reached.

## Available tools
- memory_add_fact(text, category?) -- save a new fact (returns existing if duplicate)
- memory_update_fact(factId, text, category?) -- update existing fact by id
- memory_delete_fact(factId) -- delete fact by id
- memory_search_facts(query, limit?) -- search by relevance (BM25 + filters)
- memory_list_facts(limit?) -- list recent facts (debug only)

## When to RETRIEVE
IMPORTANT: Never claim you remember something unless you searched first.

Search proactively when:
- User references past context (e.g. as I said, do you remember, last time)
- A preference, setting, or constraint could change your answer
- You are about to assume something that might have been stated before

## Search query syntax
Operators you can use inside the query string (combine freely):

1. Exact phrase -- wrap in double quotes. Acts as a MUST-match filter.
   Example query value: "error budget" deployment

2. Category filter -- prefix with cat: or #. Acts as a MUST-match filter.
   Example query value: cat:preference language
   Example query value: #project net10

3. Exclusion -- prefix token with -. Removes facts containing that token.
   Example query value: deployment -legacy

4. Plain keywords -- ranked by relevance (BM25). Supports prefix matching and light typo tolerance.
   Example query value: dotnet framework version

5. ID lookup -- paste a fact id (or partial). Strongest signal.
   Example query value: a1b2c3d4

Search is case-insensitive. An empty query returns the most recently updated facts.

## Retrieval strategy
1. Search with the user’s original phrasing first
2. If weak -- add a category filter (cat:) or exact phrase (double quotes)
3. If still weak -- compact keywords (2-6 words), synonyms, or domain entities
Keep to 2-4 searches max per turn.

## When to STORE
Store durable, future-useful facts:
- User preferences (language, tone, formatting, verbosity)
- Stable profile facts meant to be remembered (role, timezone, name)
- Long-lived goals, constraints, requirements, always/never rules
- Stable project decisions, conventions, environment settings

If unsure whether it is durable -- ask the user or do not store.

## How to STORE
IMPORTANT: Format facts for optimal search recall.
- One atomic idea per fact
- Include 2-3 compact rephrasings separated by ;
- Use stable keywords, not fluffy prose
- ALWAYS end with: tags: tag1, tag2, tag3
  Tags should be short, lowercase, stable keywords.
  Include multilingual variants and domain terms (project/tool/feature names) when relevant.
  Reflect the category in tags (e.g. category=preference => add tag preference).

<example>
memory_add_fact("User prefers concise answers; short responses; minimal verbosity. tags: preference, style, concise, verbosity", "preference")
</example>

<example>
memory_add_fact("Project uses .NET 10; target framework net10.0; dotnet 10. tags: project, dotnet, net10, framework", "project")
</example>

## Update vs Add
If new info conflicts with existing memory -- search first, then use memory_update_fact instead of creating duplicates.

## Do NOT store
- Secrets (passwords, API keys, tokens, private keys)
- Sensitive personal data (full address, payment cards)
- Ephemeral one-off details (unless user explicitly asks to remember)

## Output
Use retrieved facts naturally in your response. Do not expose internal tool JSON.
""";
    }
}
