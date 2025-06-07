using LLMTextToSql.Interfaces.Agents;

namespace LLMTextToSql.Agents
{
    public class InterpreterAgent : IInterpreterAgent
    {
        public string PreprocessQuestion(string question)
        {
            return question.Trim();
        }
    }
}
