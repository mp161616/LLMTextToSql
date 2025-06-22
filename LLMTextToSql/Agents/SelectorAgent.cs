using LLMTextToSql.Interfaces.Agents;
using System.Text.Json;

namespace LLMTextToSql.Agents
{
    public class SelectorAgent : ISelectorAgent
    {
        public IEnumerable<string> SelectRelevantTables(string question, string schemaJson)
        {
            static string Normalize(string input) =>
                input.ToLower().Replace("_", "").TrimEnd('s');

            var keywords = question.ToLower().Split(new[] { ' ', '?', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);
            var doc = JsonDocument.Parse(schemaJson);
            var tables = doc.RootElement.GetProperty("tables");

            var relevant = new List<string>();

            foreach (var table in tables.EnumerateObject())
            {
                var tableName = Normalize(table.Name);
                var columnNames = table.Value.EnumerateArray().Select(c => c.GetString()).Where(c => c != null).ToList();
                var normalizedColumns = columnNames.Select(Normalize).ToList();

                var normalizedKeywords = keywords.Select(Normalize).ToList();

                bool matchesTable = normalizedKeywords.Any(k => k.Contains(tableName));
                bool matchesColumn = normalizedKeywords.Any(k =>
                    normalizedColumns.Any(c =>
                        c.Contains(k) || k.Contains(c)
                    )
                );

                if (matchesTable || matchesColumn)
                    relevant.Add(table.Name);
            }

            return relevant.Distinct();
        }
    }
}
