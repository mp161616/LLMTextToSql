using LLMTextToSql.Interfaces;
using LLMTextToSql.Interfaces.Agents;
using LLMTextToSql.Interfaces.Services;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace LLMTextToSql.Services
{
    public class OllamaLlmService : ILlmService
    {
        private readonly HttpClient _httpClient;
        private readonly IInterpreterAgent _interpreter;
        private readonly ISelectorAgent _selector;
        private readonly IAugmenterAgent _augmenter;
        private readonly IFixerAgent _fixer;
        private readonly string _schemaJson;

        public OllamaLlmService(
            HttpClient httpClient,
            IInterpreterAgent interpreter,
            ISelectorAgent selector,
            IAugmenterAgent augmenter,
            IFixerAgent fixer)
        {
            _httpClient = httpClient;
            _interpreter = interpreter;
            _selector = selector;
            _augmenter = augmenter;
            _fixer = fixer;

            // Load the compressed schema from file
            var schemaPath = Path.Combine(AppContext.BaseDirectory, "Schemas", "pagila_compressed_schema.json");
            _schemaJson = File.ReadAllText(schemaPath);
        }

        public async Task<string> GenerateSqlAsync(string question)
        {
            // 1) Preprocess the user question
            string pre = _interpreter.PreprocessQuestion(question);

            // 2) Select relevant tables from the schema
            var relevant = _selector.SelectRelevantTables(pre, _schemaJson);

            // 3) Build a minimal prompt with only those tables
            string prompt = _augmenter.AugmentPrompt(pre, relevant, _schemaJson);

            var body = new
            {
                model = "sqlcoder",
                prompt = prompt,
                stream = false
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync("http://localhost:11434/api/generate", body);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    return $"[LLM Error] {response.StatusCode}: {err}";
                }

                var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
                string rawSql = result?.Response?.Trim() ?? "[Empty]";

                // 4) Post‐process / fix any typos
                return _fixer.FixQuery(rawSql, _schemaJson);
            }
            catch (Exception ex)
            {
                return $"[Exception] {ex.Message}";
            }
        }

        private class OllamaResponse
        {
            public string Response { get; set; }
        }
    }
}
