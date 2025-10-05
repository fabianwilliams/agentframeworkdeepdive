using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.AI;
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
    /// Gets the configured AI chat client based on Provider setting in appsettings.json.
    /// Currently only supports OpenAI.
    /// </summary>
    public static ChatClient GetChatClient()
    {
        var provider = Configuration["AI:Provider"] ?? "OpenAI";

        return provider.ToLowerInvariant() switch
        {
            "openai" => GetOpenAIChatClient(),
            _ => throw new InvalidOperationException(
                $"Unknown provider: {provider}. Currently only 'OpenAI' is supported.")
        };
    }

    /// <summary>
    /// Gets OpenAI chat client.
    /// </summary>
    public static ChatClient GetOpenAIChatClient()
    {
        var apiKey = Configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey not found in appsettings.json");
        var model = Configuration["OpenAI:Model"] ?? "gpt-4o-mini";

        return new OpenAIClient(apiKey).GetChatClient(model);
    }

    /// <summary>
    /// Gets the current provider name for display purposes.
    /// </summary>
    public static string GetProviderName()
    {
        var provider = Configuration["AI:Provider"] ?? "OpenAI";
        var model = Configuration["OpenAI:Model"] ?? "gpt-4o-mini";
        return $"{provider} ({model})";
    }
}
