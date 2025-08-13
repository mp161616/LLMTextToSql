using LLMTextToSql.Interfaces.Agents;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace LLMTextToSql.Agents
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

        public async Task<string> DecomposeChainOfThoughtAsync(string question)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{_pythonScriptPath}\" \"{EscapeArg(question)}\" \"{_schemaFilePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi)
                                ?? throw new InvalidOperationException("Could not start Python process.");

            string stdout = await process.StandardOutput.ReadToEndAsync();
            string stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (!string.IsNullOrWhiteSpace(stderr))
                throw new InvalidOperationException($"[Python Error]\n{stderr}");

            return stdout.Trim();
        }

        private static string EscapeArg(string arg)
            => arg.Replace("\"", "\\\"");
    }
}
