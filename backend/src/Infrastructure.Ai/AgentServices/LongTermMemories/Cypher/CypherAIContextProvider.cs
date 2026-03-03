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
        string agentId,
        CypherFactsStore store,
        JsonSerializerOptions? jsonSerializerOptions = null)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            throw new ArgumentException("agentId is required.", nameof(agentId));

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
# Long-term memory: Facts (scoped per Agent.Id, stored in Neo4j)
IMPORTANT: This memory is **agent-scoped**.
- Each agent (Agent.Id) has its **own isolated memory bucket** in the graph database.
- Facts stored by this agent are available across all its sessions,
  but are **not shared** with other agents.

You have long-term memory tools:
- memory_add_fact(text, category?) -> saves a fact and returns it with an id
- memory_update_fact(factId, text, category?) -> updates a fact by id
- memory_delete_fact(factId) -> deletes a fact by id
- memory_search_facts(query, limit?) -> full-text keyword search, returns matches by relevance
- memory_list_facts(limit?) -> lists most recent facts (debug, optional)

## Use memory proactively (default behavior)

### When to RETRIEVE (search first)
Run memory_search_facts proactively when:
- user asks to recall/remember, references the past ("as I said", "do you remember?", "last time")
- a preference/setting/constraint could change the best answer (language, tone, format, project conventions)
- you are about to assume something about the user/project that might have been stated before

Rule: **Do not claim you remember something unless you searched.**

### Search query language
memory_search_facts uses **Lucene full-text search** via Neo4j.
Pass **plain keywords** — the engine handles relevance ranking automatically.

**Do:**
- memory_search_facts("language preference ukrainian")
- memory_search_facts("dotnet framework version")
- memory_search_facts("deployment pipeline azure")

**Avoid:**
- Special operators like `cat:`, `#`, `-term` (they are not supported here)
- Quoted phrases (quotes are stripped before indexing)
- Punctuation or symbols

Results are ordered by Lucene relevance score (term frequency, inverse document frequency).
If no results, try rephrasing with fewer or different keywords.

### Retrieval strategy
When you need memory:
1) Search with the user's main keywords (3–6 words):
   - memory_search_facts("preference language response style", 8)
2) If results are weak, try shorter / synonym queries:
   - memory_search_facts("language response", 8)
   - memory_search_facts("response style preference", 8)
3) If still weak, search by a single strong keyword:
   - memory_search_facts("ukrainian", 8)
Keep searches to 2–4 calls max per turn.

## When to STORE (call memory_add_fact)
Store durable, future-useful facts:
- user preferences (language, tone, formatting, verbosity)
- stable profile facts explicitly meant to be remembered (role, timezone, name)
- long-lived goals, constraints, requirements, "always/never" rules
- stable project decisions / conventions / environment settings

If unsure whether it's durable: ask the user OR don't store.

## How to STORE (format for better search)
Store each fact in a **keyword-rich** format to maximise Lucene recall:
- One durable idea per fact (atomic).
- Include **2–3 compact rephrasings / aliases** separated by `;`.
- End the text with: `tags: tag1, tag2, tag3`
  - Short, stable lowercase keywords
  - Include domain keywords (project/tool/feature names)

Example fact text:
`User prefers concise answers; respond clearly and briefly. tags: preference, style, concise`

Another example:
`Project uses .NET 10; target framework net10.0; dotnet 10. tags: project, dotnet, net10, framework`

## Update vs Add
If new information conflicts with existing memory:
1) Search first (memory_search_facts with relevant keywords)
2) Update the existing fact (memory_update_fact) instead of creating duplicates.

## When NOT to store
Do NOT store:
- secrets (passwords, API keys, auth tokens, private keys)
- highly sensitive personal data (full address, payment cards, etc.)
- one-off ephemeral details (unless user explicitly asks to remember)

## Output usage
Use retrieved facts naturally in your response.
You may briefly mention you checked stored memory, but do not expose internal tool JSON.
""";
}
