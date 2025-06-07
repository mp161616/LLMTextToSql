namespace LLMTextToSql.Interfaces.Agents
{
    public interface IFixerAgent
    {
        string FixQuery(string originalQuery, string schemaJson);
    }
}
