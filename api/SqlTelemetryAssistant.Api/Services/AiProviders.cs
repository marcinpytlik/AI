
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SqlTelemetryAssistant.Api.Services;

public interface IAiRecommendationService
{
    Task<string> GetRecommendationAsync(string prompt);
}

public class OpenAiRecommendationProvider : IAiRecommendationService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _model;

    public OpenAiRecommendationProvider(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
        _apiKey = config["OpenAi:ApiKey"] ?? "";
        _model = config["OpenAi:Model"] ?? "gpt-4.1-mini";
        _baseUrl = config["OpenAi:BaseUrl"] ?? "https://api.openai.com/v1";

        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);
        }
    }

    public async Task<string> GetRecommendationAsync(string prompt)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return "Brak skonfigurowanego klucza OpenAI. Skonfiguruj OpenAi:ApiKey w appsettings.json lub zmiennych środowiskowych.";
        }

        var payload = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = "Jesteś ekspertem SQL Server (DBA), który analizuje telemetrię i podaje konkretne rekomendacje w języku polskim." },
                new { role = "user", content = prompt }
            }
        };

        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl.TrimEnd('/')}/chat/completions", payload);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        var content = root
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content ?? "(brak treści odpowiedzi modelu)";
    }
}

public class LocalLlmRecommendationProvider : IAiRecommendationService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _model;

    public LocalLlmRecommendationProvider(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
        _apiKey = config["LocalLlm:ApiKey"] ?? "";
        _model = config["LocalLlm:Model"] ?? "llama3.1";
        _baseUrl = config["LocalLlm:BaseUrl"] ?? "http://ollama:11434/v1";

        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);
        }
    }

    public async Task<string> GetRecommendationAsync(string prompt)
    {
        var payload = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = "Jesteś ekspertem SQL Server (DBA), który analizuje telemetrię i podaje konkretne rekomendacje w języku polskim." },
                new { role = "user", content = prompt }
            }
        };

        var url = $"{_baseUrl.TrimEnd('/')}/chat/completions";
        var response = await _httpClient.PostAsJsonAsync(url, payload);

        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        var content = root
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content ?? "(brak treści odpowiedzi lokalnego modelu)";
    }
}
