namespace App.Infrastructure.Ai;

public class NvidiaOptions
{
    public string BaseUrl { get; set; } = "https://integrate.api.nvidia.com/v1";
    public string ApiKey { get; set; } = default!;
    public string ChatModel { get; set; } = "nvidia/nemotron-3-nano-30b-a3b";
    public string EmbeddingModel { get; set; } = "nvidia/llama-nemotron-embed-vl-1b-v2";
}
