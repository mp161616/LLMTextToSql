namespace LLMTextToSql.Interfaces.Agents
{
    public interface IPythonSelectorAgent
    {
        Task<string> SelectRelevantSchemaAsync(string question);
    }
}
