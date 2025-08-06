using LLMTextToSql.Interfaces;
using LLMTextToSql.Interfaces.Agents;
using LLMTextToSql.Interfaces.Services;
using System.Text.Json;

namespace LLMTextToSql.Services
{
    public class OllamaLlmService : ILlmService
    {
        private readonly HttpClient _httpClient;
        private readonly IPythonSelectorAgent _selector;
        private readonly IAugmentService _augmenter;
        private readonly IFixerService _fixer;
        private readonly string _schemaJson;

        public OllamaLlmService(
            HttpClient httpClient,
            IPythonSelectorAgent selector,
            IAugmentService augmenter,
            IFixerService fixer)
        {
            _httpClient = httpClient;
            _selector = selector;
            _augmenter = augmenter;
            _fixer = fixer;

            var schemaPath = Path.Combine(AppContext.BaseDirectory, "Schemas", "pagila_compressed_schema.json");
            _schemaJson = File.ReadAllText(schemaPath);
        }

        public async Task<string> GenerateSqlAsync(string question)
        {
            string preprocessedQuestion = question.Trim();

            try
            {
               var relevantSchemaJson = await _selector.SelectRelevantSchemaAsync(preprocessedQuestion);

               return _fixer.FixQuery(relevantSchemaJson, _schemaJson);
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
