namespace App.Application.Abstractions;

public record ChatTurn(string Role, string Content);

public interface IChatCompletionService
{
    Task<string> GetCompletionAsync(IEnumerable<ChatTurn> messages, CancellationToken ct = default);
}