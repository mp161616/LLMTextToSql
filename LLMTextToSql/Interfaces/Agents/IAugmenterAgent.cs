namespace LLMTextToSql.Interfaces.Agents
{
    public interface IAugmenterAgent
    {
        string AugmentPrompt(string question, IEnumerable<string> relevantTables, string schemaJson);
    }
}
