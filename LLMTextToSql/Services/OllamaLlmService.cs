using LLMTextToSql.Interfaces;
using LLMTextToSql.Interfaces.Agents;
using LLMTextToSql.Interfaces.Services;

namespace LLMTextToSql.Services
{
    public class OllamaLlmService : ILlmService
    {
        private readonly HttpClient _httpClient;
        private readonly ISelectorAgent _selector;
        private readonly IAugmentService _augmenter;
        private readonly IFixerService _fixer;
        private readonly string _schemaJson;

        public OllamaLlmService(
            HttpClient httpClient,
            ISelectorAgent selector,
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

            var relevant = _selector.SelectRelevantTables(preprocessedQuestion, _schemaJson);

            string prompt = _augmenter.AugmentPrompt(preprocessedQuestion, relevant, _schemaJson);

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
