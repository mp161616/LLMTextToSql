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

        public async Task<string> RefineUntilExecutableAsync(
                string initialSql,
                string question,
                int maxAttempts = 4)
        {
            string currentSql = initialSql;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                // Determine the SQL verb (SELECT/UPDATE/DELETE/...)
                var trimmed = currentSql.TrimStart();
                var stmtType = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0]
                                      .ToUpperInvariant();

                try
                {
                    await using var conn = new NpgsqlConnection(_postgresConnectionString);
                    await conn.OpenAsync();

                    // Start a transaction so we never commit mutations
                    await using var tx = await conn.BeginTransactionAsync();
                    await using var cmd = new NpgsqlCommand(currentSql, conn, tx);

                    if (stmtType == "SELECT")
                    {
                        await using var reader = await cmd.ExecuteReaderAsync();
                    }
                    else
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Roll back any changes (dry‐run)
                    await tx.RollbackAsync();

                    // Success—return the clean, executable SQL
                    return currentSql;
                }
                catch (PostgresException pgEx)
                {
                    if (attempt == maxAttempts)
                        return currentSql;

                    // Let the refiner correct it, then retry
                    var errorMsg = pgEx.MessageText;
                    currentSql = await _refinerAgent.RefineAndGenerateSqlAsync(currentSql, errorMsg, question);
                }
                catch (Exception ex)
                {
                    if (attempt == maxAttempts)
                        return currentSql;

                    // Non‐SQL error—also send to refiner
                    currentSql = await _refinerAgent.RefineAndGenerateSqlAsync(currentSql, ex.Message, question);
                }
            }

            // Should never reach here, but return the last SQL anyway
            return currentSql;
        }
    }
}
