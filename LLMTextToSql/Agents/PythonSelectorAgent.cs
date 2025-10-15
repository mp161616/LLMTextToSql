using LLMTextToSql.Interfaces.Agents;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LLMTextToSql.Agents
{
    public class PythonSelectorAgent : IPythonSelectorAgent
    {
        private readonly string _pythonScriptPath;

        public PythonSelectorAgent(string pythonScriptPath)
        {
            _pythonScriptPath = pythonScriptPath;
        }

        public async Task<string> SelectRelevantSchemaAsync(string question)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{_pythonScriptPath}\" \"{question.Replace("\"", "\\\"")}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                    return "[Error] Could not start Python process.";

                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();

                await Task.WhenAll(stdoutTask, stderrTask);
                await process.WaitForExitAsync();

                string stdout = stdoutTask.Result;
                string stderr = stderrTask.Result;

                if (!string.IsNullOrWhiteSpace(stdout) && stdout.Trim().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                {
                    return stdout.Trim();
                }

                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    return $"[Python Error]\n{stderr}";
                }

                return stdout.Trim();
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new List<string> { $"[Exception] {ex.Message}" });
            }
        }

        private static string EscapeArg(string arg)
        {
            return arg.Replace("\"", "\\\"");
        }
    }
}
