using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Reflection;
using System.Text.Json;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices.LongTermMemories.Cypher;

public sealed class CypherFactsContextProvider : AIContextProvider
{
    private readonly List<AITool> _tools;
    private readonly string _memoryInstructions;

    public CypherFactsContextProvider(
        Guid agentId,
        CypherFactsStore store,
        JsonSerializerOptions? jsonSerializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(store);

        var toolset = new CypherFactsMemoryTools(store, agentId);

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
        };

        return new ValueTask<AIContext>(aiContext);
    }

    private static IEnumerable<AITool> BuildTools(CypherFactsMemoryTools toolset, JsonSerializerOptions? serializerOptions)
    {
        var t = typeof(CypherFactsMemoryTools);

        MethodInfo GetRequired(string name) =>
            t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMethodException(t.FullName, name);

        var tools = new List<AITool>
        {
            CreateTool(GetRequired(nameof(CypherFactsMemoryTools.AddFact)), toolset,
                name: "memory_add_fact",
                description: "Add a durable fact to long-term memory.",
                serializerOptions),

            CreateTool(GetRequired(nameof(CypherFactsMemoryTools.UpdateFact)), toolset,
                name: "memory_update_fact",
                description: "Update an existing fact by id.",
                serializerOptions),

            CreateTool(GetRequired(nameof(CypherFactsMemoryTools.DeleteFact)), toolset,
                name: "memory_delete_fact",
                description: "Delete a fact by id.",
                serializerOptions),

            CreateTool(GetRequired(nameof(CypherFactsMemoryTools.SearchFacts)), toolset,
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
Each agent has its own isolated fact store in the graph database. Facts persist across sessions but are NOT shared between agents.

## Available tools
- memory_add_fact(text, category?) — save a new fact
- memory_update_fact(factId, text, category?) — update existing fact by id
- memory_delete_fact(factId) — delete fact by id
- memory_search_facts(query, limit?) — full-text keyword search (Lucene)
- memory_list_facts(limit?) — list recent facts (debug)

## When to RETRIEVE
IMPORTANT: Do not claim you remember something unless you searched first.

Search proactively when:
- User references past context ("as I said", "do you remember", "last time")
- A preference, setting, or constraint could change your answer
- You are about to assume something that might have been stated before

## Search query syntax
IMPORTANT: memory_search_facts uses Lucene full-text search. Pass plain keywords only.

Do:
- memory_search_facts("language preference ukrainian")
- memory_search_facts("dotnet framework version")
- memory_search_facts("deployment pipeline azure")

Do NOT use special operators (`cat:`, `#`, `-term`, `"quoted phrases"`, punctuation) — they are stripped and ignored.

Results are ranked by Lucene relevance (TF-IDF). If no results — rephrase with fewer or different keywords.

## Retrieval strategy
1. Search with main keywords (3-6 words): `memory_search_facts("preference language style", 8)`
2. If weak — try shorter/synonym queries or alternative phrasings: memory_search_facts("response language", 8)
3. If still weak — single strong keyword: `memory_search_facts("ukrainian", 8)`
Keep to 2-4 searches max per turn.

## When to STORE
Store durable, future-useful facts:
- User preferences (language, tone, formatting, verbosity)
- Stable profile facts meant to be remembered (role, timezone, name)
- Long-lived goals, constraints, requirements, "always/never" rules
- Stable project decisions, conventions, environment settings

If unsure whether it's durable — ask the user or don't store.

## How to STORE
IMPORTANT: Format facts to maximize Lucene keyword recall.
- One atomic idea per fact
- Include 2-3 compact rephrasings/aliases separated by `;`
- Use stable keywords, not fluffy prose
- ALWAYS end with: `tags: tag1, tag2, tag3`
  - Short, lowercase, stable keywords
  - Multilingual variants when relevant
  - Domain keywords (project/tool/feature names)

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
