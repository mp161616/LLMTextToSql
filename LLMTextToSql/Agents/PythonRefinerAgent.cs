using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LLMTextToSql.Interfaces.Agents;


namespace LLMTextToSql.Agents
{
    public class PythonRefinerAgent : IPythonRefinerAgent
    {
        private readonly string _script, _schema, _dsn;
        public PythonRefinerAgent(string scriptPath, string schemaPath, string dbConn)
        {
            _script = scriptPath;
            _schema = schemaPath;
            _dsn = dbConn;
        }

        public async Task<string> RefineAndGenerateSqlAsync(string flawedSql, string err, string question)
        {
           var args = string.Join(" ",
               $"\"{_script}\"",
               $"\"{Escape(flawedSql)}\"",
               $"\"{Escape(err)}\"",
               $"\"{Escape(question)}\"",
               $"\"{Escape(_schema)}\"",
               $"\"{Escape(_dsn)}\""
           );

            var psi = new ProcessStartInfo("python", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi)
                          ?? throw new InvalidOperationException("Cannot start Python");

            string stdout = await proc.StandardOutput.ReadToEndAsync();
            string stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (!string.IsNullOrWhiteSpace(stderr))
                return $"[Python Refiner Error]\n{stderr}";

            return ExtractSql(stdout);
        }

        static string Escape(string s) => s.Replace("\"", "\\\"");
        private static readonly Regex _rxCodeBlock = new Regex(@"```sql\s*(.*?)\s*```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        private static readonly Regex _rxFallback = new Regex(@"(?i)\b(?:SELECT|UPDATE|DELETE)\b[\s\S]+?(?=(;|\z))", RegexOptions.Compiled);

        private static string ExtractSql(string text)
        {
           var codeBlockMatch = _rxCodeBlock.Match(text);
           if (codeBlockMatch.Success)
           {
                return codeBlockMatch.Groups[1].Value.Trim();
           }

            var matches = _rxFallback.Matches(text);
            if (matches.Count > 0)
            {
                var last = matches[matches.Count - 1].Value.Trim();
                return last.EndsWith(";") ? last : last + ";";
            }
            return text.Trim();
        }

    }
}
