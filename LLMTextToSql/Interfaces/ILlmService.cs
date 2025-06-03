namespace LLMTextToSql.Interfaces
{
    public interface ILlmService
    {
        Task<string> GetSqlFromPrompt(string userPrompt);
    }
}
