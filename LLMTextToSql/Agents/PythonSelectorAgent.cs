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

                string stdout = await process.StandardOutput.ReadToEndAsync();
                string stderr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (!string.IsNullOrWhiteSpace(stderr))
                    return $"[Python Error]\n{stderr}";

                string rawSql = stdout.Trim();

                var tables = new List<string> { rawSql };
                return JsonSerializer.Serialize(tables);
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
