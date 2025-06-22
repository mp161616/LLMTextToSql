namespace LLMTextToSql.Interfaces.Services
{
    public interface IFixerService
    {
        string FixQuery(string originalQuery, string schemaJson);
    }
}
