using System.Security.Cryptography;
using System.Text;
using App.Application.Abstractions;

namespace App.Infrastructure.Ai;

// Stubs SOLO para Development: permiten probar el flujo RAG y el aislamiento
// multi-tenant end-to-end sin depender de una NVIDIA_API_KEY real. Nunca se
// registran en Production (ver Program.cs).
public class StubEmbeddingService : IEmbeddingService
{
    public Task<float[]> GetEmbeddingAsync(string text, string inputType = "query", CancellationToken ct = default)
    {
        // Hash determinista: mismo texto -> mismo vector (distancia 0), lo que
        // permite verificar el aislamiento con queries idénticas al contenido.
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        var vector = new float[1024];
        for (var i = 0; i < vector.Length; i++)
            vector[i] = (bytes[i % bytes.Length] - 128f) / 128f;
        return Task.FromResult(vector);
    }
}

public class StubChatCompletionService : IChatCompletionService
{
    public Task<string> GetCompletionAsync(IEnumerable<ChatTurn> messages, CancellationToken ct = default)
    {
        var user = messages.LastOrDefault(m => m.Role == "user")?.Content ?? string.Empty;

        // El TriageService pide SOLO un JSON: se simula un triaje que clasifica
        // como Reclamo/Alta/Negativo para poder probar la notificación SignalR.
        if (user.Contains("devolvé SOLO un JSON válido", StringComparison.Ordinal))
            return Task.FromResult("{\"type\":\"Reclamo\",\"priority\":\"Alta\",\"sentiment\":\"Negativo\",\"summary\":\"Cobro duplicado reportado por el cliente, requiere atención urgente.\"}");

        return Task.FromResult($"Respuesta de prueba (sin NVIDIA): {user}");
    }
}