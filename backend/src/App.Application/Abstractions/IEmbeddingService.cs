namespace App.Application.Abstractions;

public interface IEmbeddingService
{
    // inputType: "query" al buscar, "passage" al indexar contenido.
    Task<float[]> GetEmbeddingAsync(string text, string inputType = "query", CancellationToken ct = default);
}