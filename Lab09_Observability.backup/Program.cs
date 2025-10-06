using System.Diagnostics;
using System.Diagnostics.Metrics;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

internal class Program
{
    private const string ActivitySourceName = "AgentFrameworkLabs.Lab09.Agent";
    private const string MeterName = "AgentFrameworkLabs.Lab09.Metrics";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> AgentRunCounter =
        Meter.CreateCounter<long>("gen_ai.operation.invocations", description: "Total GenAI agent runs executed.");

    private static readonly Histogram<long> InputTokensHistogram =
        Meter.CreateHistogram<long>("gen_ai.response.usage.input_tokens", unit: "{token}",
            description: "Input tokens consumed per agent response.");

    private static readonly Histogram<long> OutputTokensHistogram =
        Meter.CreateHistogram<long>("gen_ai.response.usage.output_tokens", unit: "{token}",
            description: "Output tokens produced per agent response.");

    private static readonly Histogram<long> TotalTokensHistogram =
        Meter.CreateHistogram<long>("gen_ai.response.usage.total_tokens", unit: "{token}",
            description: "Total tokens observed per agent response.");

    private static ILogger<Program>? _logger;

    private static async Task Main(string[] args)
    {
        string provider = AgentConfig.Configuration["AI:Provider"] ?? "OpenAI";
        string model = provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)
            ? AgentConfig.Configuration["OpenAI:Model"] ?? "gpt-4o-mini"
            : AgentConfig.Configuration["Ollama:Model"] ?? "unknown";

        string? connectionString = AgentConfig.Configuration["ConnectionStrings:AZURE_MONITOR_CONNECTION_STRING"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠️ Azure Monitor connection string missing; exporting only to console.");
            Console.ResetColor();
        }

        ResourceBuilder resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: "AgentFrameworkLabs.Lab09",
                serviceVersion: "1.0.0",
                serviceInstanceId: Environment.MachineName)
            .AddAttributes(new KeyValuePair<string, object>[]
            {
                new("deployment.environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "local"),
                new("gen_ai.system", provider.ToLowerInvariant()),
                new("gen_ai.request.model", model)
            });

        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        TracerProviderBuilder tracerBuilder = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddSource(ActivitySourceName)
            .AddConsoleExporter();

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            tracerBuilder = tracerBuilder.AddAzureMonitorTraceExporter(o => o.ConnectionString = connectionString);
        }

        bool exportForOpenAITracing = provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(AgentConfig.Configuration["OpenAI:ApiKey"]);

        if (exportForOpenAITracing)
        {
            tracerBuilder = tracerBuilder.AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri("https://api.openai.com/v1/traces");
                otlpOptions.Headers = $"Authorization=Bearer {AgentConfig.Configuration["OpenAI:ApiKey"]}";
            });
        }

        TracerProvider tracerProvider = tracerBuilder.Build();

        MeterProviderBuilder meterBuilder = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddMeter(MeterName)
            .AddConsoleExporter();

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            meterBuilder = meterBuilder.AddAzureMonitorMetricExporter(o => o.ConnectionString = connectionString);
        }

        MeterProvider meterProvider = meterBuilder.Build();

        using ILoggerFactory loggerFactory = LoggerFactory.Create(loggingBuilder =>
        {
            loggingBuilder.AddSimpleConsole(options => options.SingleLine = true);
            loggingBuilder.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(resourceBuilder);
                options.IncludeFormattedMessage = true;
                options.AddConsoleExporter();
                if (!string.IsNullOrWhiteSpace(connectionString))
                {
                    options.AddAzureMonitorLogExporter(o => o.ConnectionString = connectionString);
                }
            });
        });

        _logger = loggerFactory.CreateLogger<Program>();

        try
        {
            await RunAgentAsync(provider, model);
            tracerProvider.ForceFlush();
            meterProvider.ForceFlush();
        }
        finally
        {
            meterProvider.Dispose();
            tracerProvider.Dispose();
        }
    }

    private static async Task RunAgentAsync(string provider, string model)
    {
        IChatClient chatClient = AgentConfig.GetChatClient();
        Console.WriteLine($"🤖 Using: {AgentConfig.GetProviderName()}\n");

        AIAgent agent = new ChatClientAgent(
            chatClient,
            instructions: "You are Professor IrieTelemetry, a Jamaican cultural historian who documents outcomes for observability dashboards.",
            name: "IrieTelemetry");

        string userPrompt = "Produce a concise, timestamped summary of Jamaica's reggae sound system era and note two pivotal events.";

        using Activity? activity = ActivitySource.StartActivity("gen_ai.agent.run", ActivityKind.Client);
        activity?.SetTag("gen_ai.system", provider.ToLowerInvariant());
        activity?.SetTag("gen_ai.operation.name", "ReggaeSoundSystemTimeline");
        activity?.SetTag("gen_ai.request.model", model);
        activity?.SetTag("gen_ai.request.max_output_tokens", 512);
        activity?.AddEvent(new ActivityEvent(
            "gen_ai.user.message",
            tags: new ActivityTagsCollection
            {
                { "gen_ai.message.role", "user" },
                { "gen_ai.message.content", userPrompt },
                { "gen_ai.message.id", Guid.NewGuid().ToString("N") }
            }));

        Activity.Current?.SetTag("gen_ai.agent.name", "IrieTelemetry");

        AgentRunResponse response = await agent.RunAsync(userPrompt);

        AgentRunCounter.Add(1);

        if (!string.IsNullOrEmpty(response.ResponseId))
        {
            activity?.SetTag("gen_ai.response.id", response.ResponseId);
        }

        activity?.AddEvent(new ActivityEvent(
            "gen_ai.assistant.message",
            tags: new ActivityTagsCollection
            {
                { "gen_ai.message.role", "assistant" },
                { "gen_ai.message.content", response.Text ?? string.Empty },
                { "gen_ai.message.id", Guid.NewGuid().ToString("N") }
            }));

        long inputTokens = response.Usage?.InputTokenCount ?? 0;
        long outputTokens = response.Usage?.OutputTokenCount ?? 0;
        long totalTokens = response.Usage?.TotalTokenCount ?? 0;

        if (inputTokens > 0)
        {
            InputTokensHistogram.Record(inputTokens);
            activity?.SetTag("gen_ai.response.usage.input_tokens", inputTokens);
        }

        if (outputTokens > 0)
        {
            OutputTokensHistogram.Record(outputTokens);
            activity?.SetTag("gen_ai.response.usage.output_tokens", outputTokens);
        }

        if (totalTokens > 0)
        {
            TotalTokensHistogram.Record(totalTokens);
            activity?.SetTag("gen_ai.response.usage.total_tokens", totalTokens);
        }

        _logger?.LogInformation("Lab09 response {ResponseId}: {Summary}", response.ResponseId, response.Text);

        Console.WriteLine("💬 Agent Response:\n");
        Console.WriteLine(response.Text);
    }
}
