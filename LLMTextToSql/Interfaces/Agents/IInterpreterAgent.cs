namespace LLMTextToSql.Interfaces.Agents
{
    public interface IInterpreterAgent
    {
        string PreprocessQuestion(string question);
    }
}
