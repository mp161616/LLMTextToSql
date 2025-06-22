namespace LLMTextToSql.Interfaces.Services
{
    public interface IAugmentService
    {
        string AugmentPrompt(string question, IEnumerable<string> relevantTables, string schemaJson);
    }
}
