// Services/PythonRefinerAgent.cs
using LLMTextToSql.Interfaces.Agents;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LLMTextToSql.Services
{
    public class PythonRefinerAgent : IPythonRefinerAgent
    {
        private readonly string _pythonScriptPath;
        private readonly string _schemaFilePath;
        private readonly TimeSpan _timeout = TimeSpan.FromMinutes(5); // 30‐second timeout

        public PythonRefinerAgent(string pythonScriptPath, string schemaFilePath)
        {
            _pythonScriptPath = pythonScriptPath;
            _schemaFilePath = schemaFilePath;
        }

        public async Task<string> RefineAndGenerateSqlAsync(string flawedSql, string errorMessage, string question)
        {
            // Build the process info to call Python
            var psi = new ProcessStartInfo
            {
                FileName = "python",
                // Pass: refiner.py "<flawedSql>" "<errorMessage>" "<question>" "<schemaJsonPath>"
                Arguments = $"\"{_pythonScriptPath}\" \"{EscapeArg(flawedSql)}\" \"{EscapeArg(errorMessage)}\" \"{EscapeArg(question)}\" \"{_schemaFilePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return "[Error] Could not start Python refiner process.";

            // Start reading stdout and stderr asynchronously
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();

            // Create a Task that represents either: (a) process + pipe completion, or (b) timeout
            Task processExited = process.WaitForExitAsync();
            Task timeoutTask = Task.Delay(_timeout);

            // Wait for either the process to exit or the timeout to elapse
            Task firstToFinish = await Task.WhenAny(Task.WhenAll(stdoutTask, stderrTask, processExited), timeoutTask);

            if (firstToFinish == timeoutTask)
            {
                // We hit the timeout before the process finished.
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Ignore any exception that occurs during Kill()
                }
                return "[Error] Refiner timed out after 30 seconds.";
            }

            // Otherwise, the process did finish in time. Capture the outputs:
            string stdout = await stdoutTask;
            string stderr = await stderrTask;

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                return $"[Python Refiner Error]\n{stderr}";
            }

            // Heuristic: pick the first SQL-like block (i.e. starting with SELECT)
            var match = Regex.Match(stdout, @"(?i)\bSELECT\b[\s\S]+");
            return match.Success ? match.Value.Trim() : stdout.Trim();
        }

        private static string EscapeArg(string arg)
        {
            return arg.Replace("\"", "\\\"");
        }
    }
}

