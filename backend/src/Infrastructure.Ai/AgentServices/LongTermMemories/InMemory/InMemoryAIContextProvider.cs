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
            string agentId,
            InMemoryFactsStore store,
            JsonSerializerOptions? jsonSerializerOptions = null)
        {
            if (string.IsNullOrWhiteSpace(agentId))
                throw new ArgumentException("agentId is required.", nameof(agentId));

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

            // optional tool (won't break the build if ListFacts was removed)
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
# Long-term memory: Facts (scoped per Agent.Id)
IMPORTANT: This memory is **agent-scoped**.
- Each agent (Agent.Id) has its **own isolated memory bucket**.
- Facts stored by this agent are available to this same agent across all its sessions,
  but are **not shared** with other agents.

You have long-term memory tools:
- memory_add_fact(text, category?) -> saves a fact and returns it with an id
- memory_update_fact(factId, text, category?) -> updates a fact by id
- memory_delete_fact(factId) -> deletes a fact by id
- memory_search_facts(query, limit?) -> searches facts and returns matches
- memory_list_facts(limit?) -> lists recent facts (debug, optional)

## Use memory proactively (default behavior)

### When to RETRIEVE (search first)
Run memory_search_facts proactively when:
- user asks to recall/remember, references the past ("as I said", "do you remember?", "last time")
- a preference/setting/constraint could change the best answer (language, tone, format, project conventions)
- you are about to assume something about the user/project that might have been stated before

Rule: **Do not claim you remember something unless you searched.**

### Query language (powerful search syntax)
memory_search_facts supports smart queries. Use these operators when helpful:

1) **Exact phrases** (MUST match)
- Put an exact phrase in quotes:
  - memory_search_facts("\"error budget\"")
  - memory_search_facts("\"pipeline yaml\" \"azure devops\"")
Quoted phrases are treated as required: if a fact doesn't contain the phrase (in text or category), it is filtered out.

2) **Category filter** (MUST match)
- Filter by category using:
  - cat:<value>   or category:<value>
  - #<value>      (shorthand)
Examples:
- memory_search_facts("cat:preference language")
- memory_search_facts("#project net10")
If category filter is present and the fact has no category, it will not match.

3) **Exclusions** (MUST NOT match)
- Exclude token(s) with a leading minus:
  - memory_search_facts("deployment -legacy")
  - memory_search_facts("cat:constraint -temporary")
Exclusions remove facts that contain the excluded token in text or category.

4) **IDs**
- If you paste a fact id (or partial), matching is extremely strong:
  - memory_search_facts("a1b2c3d4")

### Retrieval strategy (multiple phrasings)
When you need memory:
1) Search using the user’s original phrasing (including quotes if user used them):
   - memory_search_facts("<user phrase>", 8)
2) If results are weak, try a structured query:
   - add category filter: cat:preference / #project / cat:constraint
   - add an exact phrase: "<key phrase>"
   - add exclusions: -old -deprecated
3) If still weak, search again using a compact keyword query (2–6 keywords):
   - memory_search_facts("keywords only", 8)
4) If still weak:
   - synonyms / alternative language,
   - likely tags (see below),
   - key entities (project name, feature name, tool names).
Keep searches to 2–4 calls max per turn.

### Ranking behavior (how results are ordered)
Search is relevance-ranked, not just substring:
- Strong boosts for:
  - id match
  - exact / contains matches on the main query text
  - quoted phrases
  - word-order signal (bigrams)
- Token relevance uses BM25-like weighting across:
  - fact text (primary)
  - category (lower weight)
- Light typo tolerance:
  - prefix matches and small edit-distance fuzzy matches may help,
    especially for single-word queries or when there is already a strong signal.
If your query is only filters (cat:/-term/"phrase") and has no tokens,
results are still returned (ranked mostly by recency, with phrase/id boosts).

### Concurrency / consistency note
Search works on a snapshot of facts taken under lock, then ranks without holding the lock.
This improves responsiveness under concurrent writes.

## When to STORE (call memory_add_fact)
Store durable, future-useful facts:
- user preferences (language, tone, formatting, verbosity)
- stable profile facts explicitly meant to be remembered (role, timezone, name)
- long-lived goals, constraints, requirements, "always/never" rules
- stable project decisions / conventions / environment settings

If unsure whether it’s durable: ask the user OR don’t store.

## How to STORE (format for better search)
Store each fact in a **search-friendly** format:
- One durable idea per fact (atomic).
- Include **2–3 compact rephrasings / aliases** separated by `;` (improves matching).
- Prefer stable keywords that will match token search (avoid only “fluffy” prose).
- ALWAYS end the text with:
  `tags: tag1, tag2, tag3`
  Tags should be:
  - short, stable keywords
  - preferably lowercase
  - include domain keywords (project/tool/feature names) when relevant
- If you set a category, also reflect it in tags (e.g., category=preference => tag "preference").

Example fact text:
`User prefers concise answers; respond clearly and briefly. tags: preference, style, concise`

Another example:
`Project uses .NET 10; target framework net10.0; dotnet 10. tags: project, dotnet, net10, framework`

## Update vs Add
If new information conflicts with existing memory:
1) Search first (memory_search_facts), optionally with cat:/#/quotes
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
}
