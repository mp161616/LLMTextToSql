using LLMTextToSql.Interfaces.Agents;
using System.Text.Json;

namespace LLMTextToSql.Agents
{
    public class SelectorAgent : ISelectorAgent
    {
        public IEnumerable<string> SelectRelevantTables(string question, string schemaJson)
        {
            var keywords = question.ToLower().Split(new[] { ' ', '?', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);
            var doc = JsonDocument.Parse(schemaJson);
            var root = doc.RootElement.GetProperty("tables");

            var relevant = new List<string>();

            foreach (var table in root.EnumerateObject())
            {
                var tableName = table.Name.ToLower();
                var columnNames = table.Value.EnumerateArray().Select(c => c.GetString()?.ToLower()).ToList();

                if (keywords.Any(k => tableName.Contains(k) || columnNames.Any(c => c?.Contains(k) == true)))
                {
                    relevant.Add(table.Name);
                }
            }

            return relevant.Distinct();
        }
    }
}
