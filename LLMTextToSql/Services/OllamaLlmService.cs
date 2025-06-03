using System.Net.Http.Json;
using System.Text.Json;
using LLMTextToSql.Interfaces;

namespace LLMTextToSql.Services
{
    public class OllamaLlmService : ILlmService
    {
        private readonly HttpClient _httpClient;
        private readonly string _schemaJson;

        public OllamaLlmService(HttpClient httpClient)
        {
            _httpClient = httpClient;

            // Load and compress JSON schema from file
            var schemaPath = Path.Combine(AppContext.BaseDirectory, "Schemas", "pagila_compressed_schema.json");
            _schemaJson = File.ReadAllText(schemaPath);
        }

        public async Task<string> GetSqlFromPrompt(string userPrompt)
        {
            var fullPrompt = $"""
    You are an intelligent SQL assistant.

    Given the following JSON-formatted database schema:
    {_schemaJson}

    Convert the following question into an executable SQL query.

    Question: "{userPrompt}"

    SQL query:
    """;

            var body = new
            {
                model = "sqlcoder",
                prompt = fullPrompt,
                stream = false
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync("http://localhost:11434/api/generate", body);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return $"[Error] Status {response.StatusCode}: {error}";
                }

                var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
                return result?.Response?.Trim() ?? "[Empty response]";
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
