using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Reflection;
using System.Text.Json;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices.LongTermMemories.Neo4j;

public sealed class Neo4jFactsContextProvider : AIContextProvider
{
    private readonly List<AITool> _tools;
    private readonly string _memoryInstructions;

    public Neo4jFactsContextProvider(
        Guid agentId,
        Neo4jFactsStore store,
        JsonSerializerOptions? jsonSerializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(store);

        var toolset = new Neo4jFactsMemoryTools(store, agentId);

        _tools = BuildTools(toolset, jsonSerializerOptions).ToList();
        _memoryInstructions = MemoryHint;
    }

    protected override ValueTask<AIContext> InvokingCoreAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        var aiContext = new AIContext
        {
            Instructions = _memoryInstructions is not null ? null : _memoryInstructions,
            Tools = _tools.Any() ? null : _tools
        };

        return new ValueTask<AIContext>(aiContext);
    }

    private static IEnumerable<AITool> BuildTools(Neo4jFactsMemoryTools toolset, JsonSerializerOptions? serializerOptions)
    {
        var t = typeof(Neo4jFactsMemoryTools);

        MethodInfo GetRequired(string name) =>
            t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMethodException(t.FullName, name);

        var tools = new List<AITool>
        {
            CreateTool(GetRequired(nameof(Neo4jFactsMemoryTools.AddFact)), toolset,
                name: "memory_add_fact",
                description: "Add a durable fact to long-term memory.",
                serializerOptions),

            CreateTool(GetRequired(nameof(Neo4jFactsMemoryTools.UpdateFact)), toolset,
                name: "memory_update_fact",
                description: "Update an existing fact by id.",
                serializerOptions),

            CreateTool(GetRequired(nameof(Neo4jFactsMemoryTools.DeleteFact)), toolset,
                name: "memory_delete_fact",
                description: "Delete a fact by id.",
                serializerOptions),

            CreateTool(GetRequired(nameof(Neo4jFactsMemoryTools.SearchFacts)), toolset,
                name: "memory_search_facts",
                description: "Search stored facts by plain keywords.",
                serializerOptions),
        };

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
# Long-term memory (agent-scoped, Neo4j)
Each agent has its own isolated fact store in the graph database. Facts persist across all sessions but are NOT shared between agents.
Duplicate facts (same text + category) are automatically deduplicated.

## Available tools
- memory_add_fact(text, category?) -- save a new fact (returns existing if duplicate)
- memory_update_fact(factId, text, category?) -- update existing fact by id
- memory_delete_fact(factId) -- delete fact by id
- memory_search_facts(query, limit?) -- full-text keyword search (Lucene)
- memory_list_facts(limit?) -- list recent facts (debug only)

## When to RETRIEVE
IMPORTANT: Never claim you remember something unless you searched first.

Search proactively when:
- User references past context (e.g. as I said, do you remember, last time)
- A preference, setting, or constraint could change your answer
- You are about to assume something that might have been stated before

## Search query syntax
IMPORTANT: memory_search_facts uses Lucene full-text search. Pass plain keywords only.
Do NOT use operators like cat:, #, -term, or quoted phrases -- they are stripped and ignored.
Both fact text and category fields are indexed and searchable.

Good query examples:
- language preference style
- dotnet framework version
- deployment pipeline azure

Results are ranked by Lucene relevance (TF-IDF).
An empty query returns the most recently updated facts.
If no results -- rephrase with fewer or different keywords.

## Retrieval strategy
1. Search with main keywords (3-6 words)
2. If weak -- try shorter queries, synonyms, or alternative phrasings
3. If still weak -- single strong keyword
Keep to 2-4 searches max per turn.

## When to STORE
Store durable, future-useful facts:
- User preferences (language, tone, formatting, verbosity)
- Stable profile facts meant to be remembered (role, timezone, name)
- Long-lived goals, constraints, requirements, always/never rules
- Stable project decisions, conventions, environment settings

If unsure whether it is durable -- ask the user or do not store.

## How to STORE
IMPORTANT: Format facts to maximize Lucene keyword recall.
- One atomic idea per fact
- Include 2-3 compact rephrasings separated by ;
- Use stable keywords, not fluffy prose
- ALWAYS end with: tags: tag1, tag2, tag3
  Tags should be short, lowercase, stable keywords.
  Include multilingual variants and domain terms (project/tool/feature names) when relevant.

<example>
memory_add_fact("User %Username% prefers concise answers; short responses; minimal verbosity. tags: preference, style, concise, verbosity. User: %Username%", "preference")
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
