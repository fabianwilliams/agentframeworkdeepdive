using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;

/// <summary>
/// Shared configuration helper for all labs.
/// Automatically loads appsettings.json from solution root and provides AI client.
/// </summary>
public static class AgentConfig
{
    private static IConfiguration? _configuration;

    public static IConfiguration Configuration
    {
        get
        {
            _configuration ??= new ConfigurationBuilder()
                .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), ".."))
                .AddJsonFile("appsettings.json", optional: false)
                .Build();
            return _configuration;
        }
    }

    /// <summary>
    /// Gets the configured AI chat client based on settings in appsettings.json.
    /// </summary>
    public static IChatClient GetChatClient()
    {
        var provider = Configuration["AI:Provider"] ?? "OpenAI";

        return provider.ToLowerInvariant() switch
        {
            "openai" => GetOpenAIChatClient(),
            "ollama" => GetOllamaChatClient(),
            "ollamaimage" => GetOllamaChatClientImage(),
            _ => throw new InvalidOperationException(
                $"Unknown provider: {provider}. Supported providers: OpenAI, Ollama.")
        };
    }

    /// <summary>
    /// Gets OpenAI chat client.
    /// </summary>
    public static IChatClient GetOpenAIChatClient()
    {
        var apiKey = Configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey not found in appsettings.json");
        var model = Configuration["OpenAI:Model"] ?? "gpt-4o-mini";

        ChatClient chatClient = new OpenAIClient(apiKey).GetChatClient(model);
        return chatClient.AsIChatClient();
    }

    /// <summary>
    /// Gets Ollama chat client.
    /// </summary>
    public static IChatClient GetOllamaChatClient()
    {
        var endpoint = Configuration["Ollama:Endpoint"]
            ?? throw new InvalidOperationException("Ollama:Endpoint not found in appsettings.json");
        var model = Configuration["Ollama:Model"]
            ?? throw new InvalidOperationException("Ollama:Model not found in appsettings.json");

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Invalid Ollama endpoint URI: {endpoint}");
        }

        return new OllamaChatClient(uri, model);
    }

        /// <summary>
    /// Gets Ollama chat client.
    /// </summary>
    public static IChatClient GetOllamaChatClientImage()
    {
        var endpoint = Configuration["OllamaImage:Endpoint"]
            ?? throw new InvalidOperationException("Ollama:Endpoint not found in appsettings.json");
        var model = Configuration["OllamaImage:Model"]
            ?? throw new InvalidOperationException("Ollama:Model not found in appsettings.json");

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Invalid Ollama endpoint URI: {endpoint}");
        }

        return new OllamaChatClient(uri, model);
    }

    /// <summary>
    /// Gets the current provider name for display purposes.
    /// </summary>
    public static string GetProviderName()
    {
        var provider = Configuration["AI:Provider"] ?? "OpenAI";

        return provider.ToLowerInvariant() switch
        {
            "ollama" => FormatProvider("Ollama", Configuration["Ollama:Model"] ?? "(model not set)"),
            _ => FormatProvider("OpenAI", Configuration["OpenAI:Model"] ?? "gpt-4o-mini")
        };

        static string FormatProvider(string name, string model) => $"{name} ({model})";
    }
}
