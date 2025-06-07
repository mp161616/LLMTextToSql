namespace LLMTextToSql.Interfaces.Agents
{
    /// <summary>
    /// Given a flawed SQL (Y′), an error message (E), the original question (Q),
    /// and the filtered schema, the Refiner produces a corrected SQL (Y).
    /// </summary>
    public interface IPythonRefinerAgent
    {
        /// <param name="flawedSql">The SQL that failed execution</param>
        /// <param name="errorMessage">The database engine’s error message</param>
        /// <param name="question">The original natural‐language question</param>
        /// <returns>A corrected SQL string that should execute cleanly</returns>
        Task<string> RefineAndGenerateSqlAsync(string flawedSql, string errorMessage, string question);
    }
}

