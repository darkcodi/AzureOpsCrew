using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices.LongTermMemories.InMemory
{
    public record FactDto(
        string Id,
        string Text,
        string? Category,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc);

    public record FactOperationResult(
        bool Success,
        string Message,
        FactDto? Fact = null);

    public record FactSearchResult(
        string Query,
        int TotalMatches,
        IReadOnlyList<FactDto> Facts);

    internal class FactEntity
    {
        public string Id { get; init; } = default!;
        public string Text { get; set; } = string.Empty;
        public string? Category { get; set; }
        public DateTimeOffset CreatedAtUtc { get; init; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
    }

    internal class AgentFactsBucket
    {
        public object Gate { get; } = new();
        public List<FactEntity> Facts { get; } = new();
    }

    public class InMemoryFactsStore
    {
        public static InMemoryFactsStore Shared { get; } = new();

        private const int MaxFactsPerAgent = 1000;

        private readonly ConcurrentDictionary<string, AgentFactsBucket> _buckets =
            new(StringComparer.OrdinalIgnoreCase);

        private AgentFactsBucket GetBucket(string agentId)
            => _buckets.GetOrAdd(agentId ?? string.Empty, _ => new AgentFactsBucket());

        public FactDto AddFact(string agentId, string text, string? category)
        {
            text = NormalizeText(text);
            category = NormalizeCategory(category);

            var bucket = GetBucket(agentId);
            lock (bucket.Gate)
            {
                var existing = bucket.Facts.FirstOrDefault(f =>
                    string.Equals(f.Text, text, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(f.Category ?? string.Empty, category ?? string.Empty, StringComparison.OrdinalIgnoreCase));

                if (existing is not null)
                {
                    existing.UpdatedAtUtc = DateTimeOffset.UtcNow;

                    return ToDto(existing);
                }

                var now = DateTimeOffset.UtcNow;
                var entity = new FactEntity
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Text = text,
                    Category = category,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };

                bucket.Facts.Add(entity);
                if (bucket.Facts.Count > MaxFactsPerAgent)
                {
                    bucket.Facts.Sort((a, b) => a.UpdatedAtUtc.CompareTo(b.UpdatedAtUtc));
                    var removeCount = bucket.Facts.Count - MaxFactsPerAgent;
                    bucket.Facts.RemoveRange(0, removeCount);
                }

                return ToDto(entity);
            }
        }

        public FactDto? UpdateFact(string agentId, string factId, string newText, string? categoryOrNullToKeep)
        {
            newText = NormalizeText(newText);
            var bucket = GetBucket(agentId);

            lock (bucket.Gate)
            {
                var entity = bucket.Facts.FirstOrDefault(f => string.Equals(f.Id, factId, StringComparison.OrdinalIgnoreCase));
                if (entity is null) return null;

                entity.Text = newText;

                if (categoryOrNullToKeep is not null)
                    entity.Category = NormalizeCategory(categoryOrNullToKeep);

                entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
                return ToDto(entity);
            }
        }

        public FactDto? DeleteFact(string agentId, string factId)
        {
            var bucket = GetBucket(agentId);

            lock (bucket.Gate)
            {
                var idx = bucket.Facts.FindIndex(f => string.Equals(f.Id, factId, StringComparison.OrdinalIgnoreCase));
                if (idx < 0) return null;

                var removed = bucket.Facts[idx];
                bucket.Facts.RemoveAt(idx);
                return ToDto(removed);
            }
        }

        public FactSearchResult SearchFacts(string agentId, string query, int limit = 8)
        {
            limit = Math.Clamp(limit, 1, 50);
            query = NormalizeQuery(query);

            var bucket = GetBucket(agentId);

            // 1) Take a snapshot under lock, then work without lock
            List<FactDto> factsSnapshot;
            lock (bucket.Gate)
            {
                if (bucket.Facts.Count == 0)
                    return new FactSearchResult(query, 0, Array.Empty<FactDto>());

                factsSnapshot = bucket.Facts.Select(ToDto).ToList();
            }

            // Empty query => just the most recently updated
            if (string.IsNullOrWhiteSpace(query))
            {
                var recent = factsSnapshot
                    .OrderByDescending(f => f.UpdatedAtUtc)
                    .Take(limit)
                    .ToList();

                return new FactSearchResult(query, factsSnapshot.Count, recent);
            }

            var pq = ParsedQuery.Parse(query);

            // 2) Index documents (internal doc-info)
            var docs = new List<DocInfo>(factsSnapshot.Count);
            double sumTextLen = 0;
            double sumCatLen = 0;

            foreach (var f in factsSnapshot)
            {
                var d = new DocInfo(f);
                docs.Add(d);

                sumTextLen += d.TextTokens.Length;
                sumCatLen += d.CategoryTokens.Length;
            }

            var avgTextLen = Math.Max(1.0, sumTextLen / Math.Max(1, docs.Count));
            var avgCatLen = Math.Max(1.0, sumCatLen / Math.Max(1, docs.Count));

            // 3) Query tokens for scoring:
            //    take main + words from quoted phrases (they are important)
            var scoringQueryLower = pq.MainLower;
            if (pq.PhrasesLower.Length > 0)
                scoringQueryLower = NormalizeQuery($"{scoringQueryLower} {string.Join(' ', pq.PhrasesLower)}").ToLowerInvariant();

            var orderedTerms = TokenizeLower(scoringQueryLower, maxTokens: 16);
            var uniqueTerms = orderedTerms
                .Distinct(StringComparer.Ordinal)
                .Take(16)
                .ToArray();

            // Exclusions (-term)
            var excludeTerms = pq.ExcludeLower.Length == 0
                ? Array.Empty<string>()
                : TokenizeLower(string.Join(' ', pq.ExcludeLower), maxTokens: 16)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

            // If the query consists only of filters (cat:/-term/"phrase"), tokens may be empty.
            // Then we rank by updatedAt (score = 1, +boosts if there's phrase/id).
            var queryBigrams = BuildBigrams(orderedTerms);

            // 4) DF/IDF per field (Text / Category)
            var dfText = new Dictionary<string, int>(StringComparer.Ordinal);
            var dfCat = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var term in uniqueTerms)
            {
                int dft = 0, dfc = 0;
                foreach (var d in docs)
                {
                    if (d.TextTf.ContainsKey(term)) dft++;
                    if (d.CategoryTf.ContainsKey(term)) dfc++;
                }

                if (dft > 0) dfText[term] = dft;
                if (dfc > 0) dfCat[term] = dfc;
            }

            var idfText = uniqueTerms.ToDictionary(
                t => t,
                t => dfText.TryGetValue(t, out var df) ? Idf(df, docs.Count) : 0.0,
                StringComparer.Ordinal);

            var idfCat = uniqueTerms.ToDictionary(
                t => t,
                t => dfCat.TryGetValue(t, out var df) ? Idf(df, docs.Count) : 0.0,
                StringComparer.Ordinal);

            // 5) Score and select
            var scored = new List<(DocInfo Doc, double Score)>(docs.Count);

            foreach (var d in docs)
            {
                if (!PassesFilters(d, pq, excludeTerms))
                    continue;

                var score = ScoreDoc(
                    d,
                    pq,
                    uniqueTerms,
                    queryBigrams,
                    idfText,
                    idfCat,
                    avgTextLen,
                    avgCatLen);

                if (score > 0)
                    scored.Add((d, score));
            }

            var total = scored.Count;

            var top = scored
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Doc.Fact.UpdatedAtUtc)
                .Take(limit)
                .Select(x => x.Doc.Fact)
                .ToList();

            return new FactSearchResult(query, total, top);
        }

        private static bool PassesFilters(DocInfo doc, ParsedQuery pq, string[] excludeTerms)
        {
            // category filter: cat:xxx / category:xxx / #xxx
            if (pq.CategoryFilterLower is not null)
            {
                if (string.IsNullOrWhiteSpace(doc.CategoryLower))
                    return false;

                if (!doc.CategoryLower!.Contains(pq.CategoryFilterLower, StringComparison.Ordinal))
                    return false;
            }

            // quoted phrases are MUST (usually the expected behavior)
            foreach (var phrase in pq.PhrasesLower)
            {
                if (doc.TextLower.Contains(phrase, StringComparison.Ordinal))
                    continue;

                if (doc.CategoryLower is not null && doc.CategoryLower.Contains(phrase, StringComparison.Ordinal))
                    continue;

                return false;
            }

            // exclude (-term) — drop documents by exact tokens
            foreach (var ex in excludeTerms)
            {
                if (doc.TextTf.ContainsKey(ex)) return false;
                if (doc.CategoryTf.ContainsKey(ex)) return false;
            }

            return true;
        }

        private static double ScoreDoc(
            DocInfo doc,
            ParsedQuery pq,
            string[] uniqueTerms,
            HashSet<(string A, string B)> queryBigrams,
            IReadOnlyDictionary<string, double> idfText,
            IReadOnlyDictionary<string, double> idfCat,
            double avgTextLen,
            double avgCatLen)
        {
            // Tuning knobs (for a PoC feel free to tweak)
            const double TextBoost = 1.0;
            const double CategoryBoost = 0.35;

            const double ExactTextEqualsBoost = 30.0;
            const double TextContainsBoost = 12.0;

            const double PhraseBoost = 18.0;
            const double BigramBoost = 4.0;

            const double PrefixBoost = 0.6;
            const double FuzzyBoost = 0.45;

            double score = 0;

            // 1) Very strong signal — Id
            if (!string.IsNullOrWhiteSpace(pq.Raw) &&
                doc.Fact.Id.Contains(pq.Raw, StringComparison.OrdinalIgnoreCase))
            {
                score += 1000;
            }

            bool hasPhraseSignal = false;

            // 2) Substring match on main-query (without filters)
            if (!string.IsNullOrWhiteSpace(pq.MainLower))
            {
                if (doc.TextLower.Equals(pq.MainLower, StringComparison.Ordinal))
                {
                    score += ExactTextEqualsBoost;
                    hasPhraseSignal = true;
                }
                else if (doc.TextLower.Contains(pq.MainLower, StringComparison.Ordinal))
                {
                    score += TextContainsBoost;
                    hasPhraseSignal = true;
                }
            }

            // 3) quoted phrases
            foreach (var phrase in pq.PhrasesLower)
            {
                if (doc.TextLower.Contains(phrase, StringComparison.Ordinal))
                {
                    score += PhraseBoost;
                    hasPhraseSignal = true;
                }
                else if (doc.CategoryLower is not null && doc.CategoryLower.Contains(phrase, StringComparison.Ordinal))
                {
                    score += PhraseBoost * 0.6;
                    hasPhraseSignal = true;
                }
            }

            // 4) Bonus for order (bigrams) — only for TextTokens
            if (queryBigrams.Count > 0 && doc.TextTokens.Length >= 2)
            {
                int hits = 0;
                for (int i = 0; i < doc.TextTokens.Length - 1; i++)
                {
                    if (queryBigrams.Contains((doc.TextTokens[i], doc.TextTokens[i + 1])))
                        hits++;
                }

                if (hits > 0)
                {
                    score += hits * BigramBoost;
                    hasPhraseSignal = true;
                }
            }

            // If there are no query tokens (only cat:/-term/"phrase") — include the document;
            // ranking will be by updatedAt (and phrase/id already provided a boost).
            if (uniqueTerms.Length == 0)
                return score > 0 ? score : 1.0;

            bool hasExactHit = false;
            int matchedTerms = 0;

            // 5) BM25 over tokens (+ gentle typo/prefix handling)
            foreach (var term in uniqueTerms)
            {
                bool matchedThis = false;

                if (doc.TextTf.TryGetValue(term, out var tfText) && tfText > 0)
                {
                    matchedThis = true;
                    hasExactHit = true;

                    var idf = idfText.TryGetValue(term, out var v) ? v : 0.0;
                    score += TextBoost * idf * Bm25(tfText, doc.TextTokens.Length, avgTextLen);
                }

                if (doc.CategoryTf.TryGetValue(term, out var tfCat) && tfCat > 0)
                {
                    matchedThis = true;
                    hasExactHit = true;

                    var idf = idfCat.TryGetValue(term, out var v) ? v : 0.0;
                    score += CategoryBoost * idf * Bm25(tfCat, doc.CategoryTokens.Length, avgCatLen);
                }

                // Enable approximate matching only if:
                // - we already have some strong signal (phrase/exact),
                // - or the query is single-word (common "typo" case)
                bool allowApprox = hasExactHit || hasPhraseSignal || uniqueTerms.Length == 1;

                if (!matchedThis && allowApprox && term.Length >= 3)
                {
                    var idf = Math.Max(
                        idfText.TryGetValue(term, out var it) ? it : 0.0,
                        idfCat.TryGetValue(term, out var ic) ? ic : 0.0);

                    if (idf <= 0) idf = 0.5;

                    if (HasPrefix(doc.TextTokens, term) || HasPrefix(doc.CategoryTokens, term))
                    {
                        matchedThis = true;
                        score += PrefixBoost * idf;
                    }
                    else if (term.Length >= 4)
                    {
                        var maxDist = term.Length <= 5 ? 1 : 2;
                        if (HasFuzzy(doc.TextTokens, term, maxDist) || HasFuzzy(doc.CategoryTokens, term, maxDist))
                        {
                            matchedThis = true;
                            score += FuzzyBoost * idf;
                        }
                    }
                }

                if (matchedThis) matchedTerms++;
            }

            // 6) Bonus for query coverage (the more words matched, the better)
            var coverage = (double)matchedTerms / uniqueTerms.Length;
            score *= 1.0 + coverage * 0.25;

            if (coverage >= 0.999 && uniqueTerms.Length > 1)
                score += 6.0;

            return score;
        }

        // -----------------------------
        // Helpers / primitives
        // -----------------------------

        private sealed class DocInfo
        {
            public FactDto Fact { get; }
            public string TextLower { get; }
            public string? CategoryLower { get; }

            public string[] TextTokens { get; }
            public string[] CategoryTokens { get; }

            public Dictionary<string, int> TextTf { get; }
            public Dictionary<string, int> CategoryTf { get; }

            public DocInfo(FactDto fact)
            {
                Fact = fact;
                TextLower = (fact.Text ?? string.Empty).ToLowerInvariant();
                CategoryLower = fact.Category?.ToLowerInvariant();

                TextTokens = TokenizeLower(TextLower, maxTokens: 256);
                CategoryTokens = string.IsNullOrWhiteSpace(CategoryLower)
                    ? Array.Empty<string>()
                    : TokenizeLower(CategoryLower!, maxTokens: 32);

                TextTf = BuildTf(TextTokens);
                CategoryTf = BuildTf(CategoryTokens);
            }
        }

        private sealed record ParsedQuery(
            string Raw,
            string RawLower,
            string MainText,
            string MainLower,
            string? CategoryFilterLower,
            string[] PhrasesLower,
            string[] ExcludeLower)
        {
            public static ParsedQuery Parse(string raw)
            {
                raw = NormalizeQuery(raw);
                var rawLower = raw.ToLowerInvariant();

                // Extract "quoted phrases"
                var phrases = new List<string>();
                var withoutPhrases = PhraseRegex.Replace(raw, m =>
                {
                    var p = NormalizeQuery(m.Groups[1].Value);
                    if (!string.IsNullOrWhiteSpace(p))
                        phrases.Add(p.ToLowerInvariant());
                    return " ";
                });

                var parts = withoutPhrases.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                string? category = null;
                var include = new List<string>();
                var exclude = new List<string>();

                foreach (var part in parts)
                {
                    if (part.Length == 0) continue;

                    if (part.StartsWith("category:", StringComparison.OrdinalIgnoreCase) ||
                        part.StartsWith("cat:", StringComparison.OrdinalIgnoreCase))
                    {
                        var idx = part.IndexOf(':');
                        if (idx >= 0 && idx < part.Length - 1)
                        {
                            var val = NormalizeQuery(part[(idx + 1)..]);
                            if (!string.IsNullOrWhiteSpace(val))
                                category = val;
                        }
                        continue;
                    }

                    if (part[0] == '#' && part.Length > 1)
                    {
                        var val = NormalizeQuery(part[1..]);
                        if (!string.IsNullOrWhiteSpace(val))
                            category ??= val;
                        continue;
                    }

                    if (part[0] == '-' && part.Length > 1)
                    {
                        var val = NormalizeQuery(part[1..]);
                        if (!string.IsNullOrWhiteSpace(val))
                            exclude.Add(val.ToLowerInvariant());
                        continue;
                    }

                    include.Add(part);
                }

                var main = NormalizeQuery(string.Join(' ', include));
                var mainLower = main.ToLowerInvariant();
                var catLower = string.IsNullOrWhiteSpace(category) ? null : category.ToLowerInvariant();

                return new ParsedQuery(
                    Raw: raw,
                    RawLower: rawLower,
                    MainText: main,
                    MainLower: mainLower,
                    CategoryFilterLower: catLower,
                    PhrasesLower: phrases.ToArray(),
                    ExcludeLower: exclude.ToArray());
            }
        }

        private static readonly Regex PhraseRegex = new("\"([^\"]+)\"", RegexOptions.Compiled);
        private static readonly Regex WhitespaceCollapse = new(@"\s+", RegexOptions.Compiled);

        private static string NormalizeQuery(string? q)
        {
            q = (q ?? string.Empty).Trim();
            return q.Length == 0 ? string.Empty : WhitespaceCollapse.Replace(q, " ");
        }

        private static string[] TokenizeLower(string lowerText, int maxTokens)
        {
            if (string.IsNullOrWhiteSpace(lowerText))
                return Array.Empty<string>();

            var parts = TokenSplit.Split(lowerText); // TokenSplit already exists in your codebase
            var tokens = new List<string>(Math.Min(parts.Length, maxTokens));

            foreach (var p in parts)
            {
                if (p.Length < 2) continue;

                tokens.Add(p);
                if (tokens.Count >= maxTokens) break;
            }

            return tokens.ToArray();
        }

        private static Dictionary<string, int> BuildTf(string[] tokens)
        {
            var tf = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var t in tokens)
            {
                if (tf.TryGetValue(t, out var c)) tf[t] = c + 1;
                else tf[t] = 1;
            }
            return tf;
        }

        private static HashSet<(string A, string B)> BuildBigrams(string[] orderedTerms)
        {
            var hs = new HashSet<(string, string)>();
            if (orderedTerms.Length < 2) return hs;

            for (int i = 0; i < orderedTerms.Length - 1; i++)
                hs.Add((orderedTerms[i], orderedTerms[i + 1]));

            return hs;
        }

        private static double Idf(int df, int n)
        {
            // BM25 idf: log(1 + (n - df + 0.5) / (df + 0.5))
            return Math.Log(1.0 + (n - df + 0.5) / (df + 0.5));
        }

        private static double Bm25(int tf, int docLen, double avgDocLen, double k1 = 1.2, double b = 0.75)
        {
            if (tf <= 0) return 0;
            if (avgDocLen <= 0) avgDocLen = 1;

            var denom = tf + k1 * (1 - b + b * docLen / avgDocLen);
            return (tf * (k1 + 1)) / denom;
        }

        private static bool HasPrefix(string[] tokens, string prefix)
        {
            foreach (var t in tokens)
            {
                if (t.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool HasFuzzy(string[] tokens, string term, int maxDist)
        {
            if (maxDist <= 0) return false;

            var first = term[0];
            foreach (var w in tokens)
            {
                if (w.Length < 2) continue;
                if (w[0] != first) continue;
                if (Math.Abs(w.Length - term.Length) > maxDist) continue;

                if (LevenshteinWithin(w, term, maxDist))
                    return true;
            }
            return false;
        }

        private static bool LevenshteinWithin(string a, string b, int maxDist)
        {
            if (a.Equals(b, StringComparison.Ordinal)) return true;
            if (maxDist <= 0) return false;

            int la = a.Length, lb = b.Length;
            if (Math.Abs(la - lb) > maxDist) return false;

            if (maxDist == 1)
            {
                if (la > lb) (a, b, la, lb) = (b, a, lb, la);
                return OneEditAwayOrEqual(a, b); // b is longer or equal
            }

            // Ensure b is longer for arrays
            if (lb < la) (a, b, la, lb) = (b, a, lb, la);

            var prev = new int[lb + 1];
            var curr = new int[lb + 1];

            for (int j = 0; j <= lb; j++) prev[j] = j;

            for (int i = 1; i <= la; i++)
            {
                curr[0] = i;
                int rowMin = curr[0];
                char ca = a[i - 1];

                for (int j = 1; j <= lb; j++)
                {
                    int cost = ca == b[j - 1] ? 0 : 1;

                    int v = prev[j] + 1;          // delete
                    int ins = curr[j - 1] + 1;    // insert
                    if (ins < v) v = ins;

                    int sub = prev[j - 1] + cost; // substitute
                    if (sub < v) v = sub;

                    curr[j] = v;
                    if (v < rowMin) rowMin = v;
                }

                if (rowMin > maxDist)
                    return false;

                (prev, curr) = (curr, prev);
            }

            return prev[lb] <= maxDist;
        }

        private static bool OneEditAwayOrEqual(string shorterOrEqual, string longerOrEqual)
        {
            // distance <= 1
            int ls = shorterOrEqual.Length;
            int ll = longerOrEqual.Length;

            if (ls == ll)
            {
                int diff = 0;
                for (int i = 0; i < ls; i++)
                {
                    if (shorterOrEqual[i] != longerOrEqual[i])
                    {
                        diff++;
                        if (diff > 1) return false;
                    }
                }
                return true;
            }

            if (ll - ls == 1)
            {
                int i = 0, j = 0;
                int diff = 0;

                while (i < ls && j < ll)
                {
                    if (shorterOrEqual[i] == longerOrEqual[j])
                    {
                        i++; j++;
                        continue;
                    }

                    diff++;
                    if (diff > 1) return false;

                    j++; // skip one char in longer
                }

                return true;
            }

            return false;
        }

        public IReadOnlyList<FactDto> ListFacts(string agentId, int limit = 50)
        {
            limit = Math.Clamp(limit, 1, 200);
            var bucket = GetBucket(agentId);

            lock (bucket.Gate)
            {
                return bucket.Facts
                    .OrderByDescending(f => f.UpdatedAtUtc)
                    .Take(limit)
                    .Select(ToDto)
                    .ToList();
            }
        }

        private static FactDto ToDto(FactEntity f)
            => new(f.Id, f.Text, f.Category, f.CreatedAtUtc, f.UpdatedAtUtc);

        private static string NormalizeText(string? text)
        {
            text = (text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Fact text cannot be empty.", nameof(text));
            return text;
        }

        private static string? NormalizeCategory(string? category)
        {
            category = category?.Trim();
            return string.IsNullOrWhiteSpace(category) ? null : category;
        }

        private static readonly Regex TokenSplit = new(@"[\W_]+", RegexOptions.Compiled);
    }
}
