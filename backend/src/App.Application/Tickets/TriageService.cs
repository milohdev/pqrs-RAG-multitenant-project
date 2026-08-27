using System.Text.Json;
using App.Application.Abstractions;
using App.Domain.Entities;

namespace App.Application.Tickets;

public record TriageResult(TicketType Type, TicketPriority Priority, Sentiment Sentiment, string? Summary);

public class TriageService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private readonly IChatCompletionService _chatService;

    public TriageService(IChatCompletionService chatService) => _chatService = chatService;

    public async Task<TriageResult> ClassifyAsync(string subject, string description, CancellationToken ct = default)
    {
        var prompt =
            "Analizá el siguiente PQRS y devolvé SOLO un JSON válido, sin texto adicional, con este formato exacto:\n" +
            "{\"type\":\"Peticion|Queja|Reclamo|Sugerencia\",\"priority\":\"Baja|Media|Alta\"," +
            "\"sentiment\":\"Positivo|Neutro|Negativo\",\"summary\":\"resumen de 1 a 2 oraciones\"}\n\n" +
            $"Asunto: {subject}\nDescripción: {description}";

        var raw = await _chatService.GetCompletionAsync(new[] { new ChatTurn("user", prompt) }, ct);

        try
        {
            var parsed = JsonSerializer.Deserialize<TriageJson>(raw, JsonOpts)!;
            return new TriageResult(
                Enum.Parse<TicketType>(parsed.Type, true),
                Enum.Parse<TicketPriority>(parsed.Priority, true),
                Enum.Parse<Sentiment>(parsed.Sentiment, true),
                parsed.Summary);
        }
        catch
        {
            // Si el LLM no devuelve JSON válido, el ticket no se pierde: queda
            // con valores por defecto y el agente puede reclasificar a mano.
            return new TriageResult(TicketType.Peticion, TicketPriority.Media, Sentiment.Neutro, null);
        }
    }

    private record TriageJson(string Type, string Priority, string Sentiment, string Summary);
}