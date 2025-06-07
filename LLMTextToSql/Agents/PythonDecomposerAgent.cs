using LLMTextToSql.Interfaces.Agents;
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LLMTextToSql.Services
{
    public class PythonDecomposerAgent : IPythonDecomposerAgent
    {
        private readonly string _pythonScriptPath;
        private readonly string _schemaFilePath;

        public PythonDecomposerAgent(string pythonScriptPath, string schemaFilePath)
        {
            _pythonScriptPath = pythonScriptPath;
            _schemaFilePath = schemaFilePath;
        }

        public async Task<string> DecomposeAndGenerateFinalSqlAsync(string question)
        {
            // Build a ProcessStartInfo to execute `python decomposer.py "question" "schema.json"`
            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{_pythonScriptPath}\" \"{EscapeArg(question)}\" \"{_schemaFilePath}\"",
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

            // Extract only the final SQL from the chain‐of‐thought output
            var match = Regex.Match(stdout, @"Final SQL:\s*(.+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return match.Success
                ? match.Groups[1].Value.Trim()
                : "[Error] Could not extract Final SQL from Python output.";
        }

        private static string EscapeArg(string arg)
        {
            return arg.Replace("\"", "\\\"");
        }
    }
}

