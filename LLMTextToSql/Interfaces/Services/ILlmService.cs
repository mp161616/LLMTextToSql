namespace LLMTextToSql.Interfaces.Services
{
    public interface ILlmService
    {
        Task<string> GenerateSqlAsync(string question);
    }
}
