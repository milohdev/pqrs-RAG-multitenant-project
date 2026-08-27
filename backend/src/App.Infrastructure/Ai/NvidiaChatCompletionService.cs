using System.Net.Http.Json;
using System.Text.Json;
using App.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace App.Infrastructure.Ai;

public class NvidiaChatCompletionService : IChatCompletionService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;
    private readonly NvidiaOptions _options;

    public NvidiaChatCompletionService(HttpClient http, IOptions<NvidiaOptions> options)
    {
        _http = http; _options = options.Value;
    }

    public async Task<string> GetCompletionAsync(IEnumerable<ChatTurn> messages, CancellationToken ct = default)
    {
        var payload = new
        {
            model = _options.ChatModel,
            messages = messages.Select(m => new { role = m.Role, content = m.Content })
        };

        using var response = await _http.PostAsJsonAsync("chat/completions", payload, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOpts, ct);
        return body?.Choices.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    private record ChatCompletionResponse(List<Choice> Choices);
    private record Choice(ChatMessage Message);
    private record ChatMessage(string Content);
}