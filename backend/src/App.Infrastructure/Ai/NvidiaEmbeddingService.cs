using System.Net.Http.Json;
using System.Text.Json;
using App.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace App.Infrastructure.Ai;

public class NvidiaEmbeddingService : IEmbeddingService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // pgvector limita los índices (HNSW/IVFFlat) a 2000 dimensiones. El modelo
    // de embeddings (llama-nemotron-embed-vl-1b-v2) devuelve 2048; se truncan
    // las últimas 48 para poder indexarlo. Igual al indexar y al buscar.
    private const int VectorDimensions = 2000;

    private readonly HttpClient _http;
    private readonly NvidiaOptions _options;

    public NvidiaEmbeddingService(HttpClient http, IOptions<NvidiaOptions> options)
    {
        _http = http; _options = options.Value;
    }

    public async Task<float[]> GetEmbeddingAsync(string text, string inputType = "query", CancellationToken ct = default)
    {
        var payload = new
        {
            model = _options.EmbeddingModel,
            input = text,
            input_type = inputType
        };

        using var response = await _http.PostAsJsonAsync("embeddings", payload, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(JsonOpts, ct);
        var embedding = body?.Data.FirstOrDefault()?.Embedding ?? Array.Empty<float>();
        return embedding.Length > VectorDimensions ? embedding.Take(VectorDimensions).ToArray() : embedding;
    }

    private record EmbeddingResponse(List<EmbeddingItem> Data);
    private record EmbeddingItem(float[] Embedding);
}