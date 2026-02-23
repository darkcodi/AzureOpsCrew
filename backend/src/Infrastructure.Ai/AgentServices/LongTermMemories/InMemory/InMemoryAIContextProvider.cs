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
- user asks to recall/remember, references the past ("як я казав", "ти пам'ятаєш", "минулого разу")
- a preference/setting/constraint could change the best answer (language, tone, format, project conventions)
- you are about to assume something about the user/project that might have been stated before

Rule: **Do not claim you remember something unless you searched.**

### Retrieval strategy (multiple phrasings)
When you need memory:
1) Search using the user’s original phrasing:
   - memory_search_facts("<user phrase>", 8)
2) If results are weak, search again using a compact keyword query (2–6 keywords):
   - memory_search_facts("keywords only", 8)
3) If still weak, search using:
   - synonyms / alternative language (UA/EN),
   - likely tags (see below),
   - key entities (project name, feature name, tool names).
Keep searches to 2–4 calls max per turn.

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
- ALWAYS end the text with:
  `tags: tag1, tag2, tag3`
  Tags should be:
  - short, stable keywords
  - preferably lowercase
  - may include both UA and EN variants (helps bilingual search)
  - include domain keywords (project/tool/feature names) when relevant

Example fact text:
`User prefers concise answers in Ukrainian; respond in Ukrainian; мова: українська. tags: preference, language, ukrainian, ua, concise`

Another example:
`Project uses .NET 10; target framework net10.0; dotnet 10. tags: project, dotnet, net10, framework`

## Update vs Add
If new information conflicts with existing memory:
1) Search first (memory_search_facts)
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
