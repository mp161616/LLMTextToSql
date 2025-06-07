using LLMTextToSql.Interfaces.Agents;
using System.Text.Json;

namespace LLMTextToSql.Agents
{
    public class AugmenterAgent : IAugmenterAgent
    {
        public string AugmentPrompt(string question, IEnumerable<string> relevantTables, string schemaJson)
        {
            var relevantSchema = ExtractRelevantSchema(relevantTables, schemaJson);

            return $"""
        You are an intelligent SQL assistant.

        Use the following subset of a database schema:
        {relevantSchema}

        Convert the question below into an executable SQL query:

        Question: "{question}"

        SQL:
        """;
        }

        private string ExtractRelevantSchema(IEnumerable<string> tableNames, string fullSchemaJson)
        {
            var doc = JsonDocument.Parse(fullSchemaJson);
            var root = doc.RootElement;
            var tables = root.GetProperty("tables");
            var relevant = new Dictionary<string, List<string>>();

            foreach (var name in tableNames)
            {
                if (tables.TryGetProperty(name, out var columns))
                {
                    relevant[name] = columns.EnumerateArray().Select(c => c.GetString()).ToList();
                }
            }

            return JsonSerializer.Serialize(new { tables = relevant });
        }
    }
}
