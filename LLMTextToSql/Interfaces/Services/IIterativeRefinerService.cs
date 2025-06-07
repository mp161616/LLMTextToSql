// Interfaces/Services/IIterativeRefinerService.cs
using System.Threading.Tasks;

namespace LLMTextToSql.Interfaces.Services
{
    /// <summary>
    /// Attempts to execute an initial SQL on the database. If it fails,
    /// calls the Refiner agent up to N times, each time re‐attempting execution,
    /// until the SQL finally succeeds or the max iteration count is reached.
    /// </summary>
    public interface IIterativeRefinerService
    {
        /// <summary>
        /// Given an initial (possibly flawed) SQL, the original question, and a max number of attempts,
        /// this method will:
        ///   1. Try to run the SQL on Postgres
        ///   2. If successful, return that SQL
        ///   3. If it fails, call IRefinerAgent to get a “fixed SQL,” then loop up to maxAttempts
        ///   4. After maxAttempts, return whatever SQL we have (successful or not)
        /// </summary>
        /// <param name="initialSql">The first SQL to try.</param>
        /// <param name="question">The original natural‐language question.</param>
        /// <param name="maxAttempts">How many times to refine before giving up.</param>
        /// <returns>
        /// Either the first SQL that executed successfully, or the last refined SQL if no success.
        /// </returns>
        Task<string> RefineUntilExecutableAsync(string initialSql, string question, int maxAttempts = 3);
    }
}
