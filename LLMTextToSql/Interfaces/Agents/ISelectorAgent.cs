namespace LLMTextToSql.Interfaces.Agents
{
    public interface ISelectorAgent
    {
        IEnumerable<string> SelectRelevantTables(string question, string schemaJson);
    }
}
