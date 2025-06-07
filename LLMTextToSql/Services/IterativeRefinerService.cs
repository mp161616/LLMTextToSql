// Services/IterativeRefinerService.cs
using LLMTextToSql.Interfaces.Agents;
using LLMTextToSql.Interfaces.Services;
using Npgsql;            // Make sure you have installed the Npgsql NuGet package
using System;
using System.Threading.Tasks;

namespace LLMTextToSql.Services
{
    public class IterativeRefinerService : IIterativeRefinerService
    {
        private readonly IPythonRefinerAgent _refinerAgent;
        private readonly string _postgresConnectionString;

        public IterativeRefinerService(IPythonRefinerAgent refinerAgent, string postgresConnectionString)
        {
            _refinerAgent = refinerAgent;
            _postgresConnectionString = postgresConnectionString;
        }

        public async Task<string> RefineUntilExecutableAsync(string initialSql, string question, int maxAttempts = 3)
        {
            // Start with the initial SQL guess
            string currentSql = initialSql;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    // 1) Try running currentSql on Postgres
                    await using var conn = new NpgsqlConnection(_postgresConnectionString);
                    await conn.OpenAsync();

                    await using var cmd = new NpgsqlCommand(currentSql, conn);
                    // Using ExecuteReaderAsync() even for SELECT is fine; if it's a non‐SELECT, use ExecuteNonQueryAsync()
                    await using var reader = await cmd.ExecuteReaderAsync();

                    // If we get here with no exception, the SQL is valid and ran successfully.
                    return currentSql;
                }
                catch (PostgresException pgEx)
                {
                    // 2) SQL failed—extract the Postgres error message
                    string errorMsg = pgEx.MessageText;
                    // (You could also include pgEx.SqlState or full pgEx.ToString() if you want more detail.)

                    if (attempt == maxAttempts)
                    {
                        // Last attempt—return whatever SQL we have, even if still invalid.
                        return currentSql;
                    }

                    // 3) Call Python Refiner to get a “fixed” version for the next iteration
                    currentSql = await _refinerAgent.RefineAndGenerateSqlAsync(currentSql, errorMsg, question);
                    // Loop again with new currentSql
                }
                catch (Exception ex)
                {
                    // Some non‐Postgres exception occurred (e.g. network issue). 
                    // You can choose to break or treat it similarly as a refineable error.
                    if (attempt == maxAttempts)
                    {
                        return currentSql;
                    }
                    // For simplicity, send the entire exception message to the Refiner.
                    currentSql = await _refinerAgent.RefineAndGenerateSqlAsync(currentSql, ex.Message, question);
                }
            }

            // If we somehow exit the loop (which we won’t, due to return inside), return the last SQL anyway
            return currentSql;
        }
    }
}
