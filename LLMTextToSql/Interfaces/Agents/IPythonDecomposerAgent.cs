namespace LLMTextToSql.Interfaces.Agents
{
    public interface IPythonDecomposerAgent
    {
        /// <summary>
        /// Uses an external Python script (decomposer.py) to break down the question
        /// into chain‐of‐thought reasoning and return the final SQL only.
        /// </summary>
        Task<string> DecomposeAndGenerateFinalSqlAsync(string question);
    }
}

