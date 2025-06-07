using LLMTextToSql.Interfaces.Agents;

namespace LLMTextToSql.Agents
{
    using System.Text.Json;
    using System.Text.RegularExpressions;

    public class FixerAgent : IFixerAgent
    {
        private Dictionary<string, HashSet<string>> _schemaMap = new();

        public string FixQuery(string originalQuery, string schemaJson)
        {
            LoadSchema(schemaJson);

            var tokens = Regex.Matches(originalQuery, "\\b\\w+\\b").Select(m => m.Value).Distinct();

            foreach (var token in tokens)
            {
                bool found = _schemaMap.Values.Any(columns => columns.Contains(token.ToLower()));
                if (!found)
                {
                    foreach (var columns in _schemaMap.Values)
                    {
                        var closest = columns.OrderBy(c => Levenshtein(c, token.ToLower())).FirstOrDefault();
                        if (closest != null && Levenshtein(closest, token.ToLower()) <= 2)
                        {
                            originalQuery = Regex.Replace(originalQuery, $"\\b{token}\\b", closest, RegexOptions.IgnoreCase);
                            break;
                        }
                    }
                }
            }

            return originalQuery;
        }

        private void LoadSchema(string schemaJson)
        {
            _schemaMap.Clear();
            var parsed = JsonDocument.Parse(schemaJson).RootElement.GetProperty("tables");
            foreach (var table in parsed.EnumerateObject())
            {
                var columnSet = table.Value.EnumerateArray().Select(c => c.GetString()?.ToLower()).ToHashSet();
                _schemaMap[table.Name.ToLower()] = columnSet;
            }
        }

        private int Levenshtein(string a, string b)
        {
            var dp = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) dp[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                    dp[i, j] = (a[i - 1] == b[j - 1])
                        ? dp[i - 1, j - 1]
                        : 1 + Math.Min(dp[i - 1, j - 1], Math.Min(dp[i, j - 1], dp[i - 1, j]));
            return dp[a.Length, b.Length];
        }
    }
}