using System.ComponentModel;
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

    private static readonly Counter<long> ToolCallCounter =
        Meter.CreateCounter<long>("gen_ai.tool.invocations", description: "Total tool invocations by agents.");

    private static ILogger<Program>? _logger;

    [Description("Get historical events from Jamaica's reggae and sound system era by year.")]
    static string GetReggaeHistoricalEvent(
        [Description("The year to query (e.g., 1950, 1970, 1980)")] int year)
    {
        using Activity? activity = ActivitySource.StartActivity("gen_ai.tool.call", ActivityKind.Internal);
        activity?.SetTag("gen_ai.tool.name", "GetReggaeHistoricalEvent");
        activity?.SetTag("gen_ai.tool.parameter.year", year);

        ToolCallCounter.Add(1, new KeyValuePair<string, object?>("tool.name", "GetReggaeHistoricalEvent"));

        _logger?.LogInformation("🔧 Tool called: GetReggaeHistoricalEvent(year={Year})", year);

        var events = new Dictionary<int, string>
        {
            [1950] = "Sound systems emerged in Kingston, featuring DJs like Duke Reid and Coxsone Dodd.",
            [1962] = "Jamaica gained independence. Ska music dominated the airwaves.",
            [1968] = "Rocksteady evolved into reggae. The Wailers released 'Soul Rebel'.",
            [1973] = "Bob Marley & The Wailers released 'Catch a Fire', bringing reggae to international audiences.",
            [1976] = "Smile Jamaica Concert. Bob Marley survived assassination attempt.",
            [1978] = "One Love Peace Concert - Bob Marley united political rivals Michael Manley and Edward Seaga on stage.",
            [1980] = "Bob Marley's final concert at Madison Square Garden.",
            [1981] = "Bob Marley passed away. Reggae's global influence continued to grow."
        };

        string result = events.TryGetValue(year, out var eventInfo)
            ? eventInfo
            : $"No major documented reggae event for {year}. Try years: {string.Join(", ", events.Keys)}";

        activity?.SetTag("gen_ai.tool.result.length", result.Length);
        activity?.AddEvent(new ActivityEvent("gen_ai.tool.completed",
            tags: new ActivityTagsCollection { { "result.preview", result[..Math.Min(50, result.Length)] } }));

        return result;
    }

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
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ Azure Monitor trace exporter configured");
            Console.ResetColor();
        }

        TracerProvider tracerProvider = tracerBuilder.Build();

        MeterProviderBuilder meterBuilder = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddMeter(MeterName)
            .AddConsoleExporter();

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            meterBuilder = meterBuilder.AddAzureMonitorMetricExporter(o => o.ConnectionString = connectionString);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ Azure Monitor metric exporter configured");
            Console.ResetColor();
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
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("✅ Azure Monitor log exporter configured");
                    Console.ResetColor();
                }
            });
        });

        _logger = loggerFactory.CreateLogger<Program>();
        Console.WriteLine();

        try
        {
            await RunAgentAsync(provider, model);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n🔄 Flushing telemetry to exporters...");
            Console.ResetColor();

            bool traceFlushed = tracerProvider.ForceFlush();
            bool metricFlushed = meterProvider.ForceFlush();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✅ Trace flush: {(traceFlushed ? "Success" : "Failed")}");
            Console.WriteLine($"✅ Metric flush: {(metricFlushed ? "Success" : "Failed")}");
            Console.ResetColor();

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n⏳ Waiting 5 seconds for Azure Monitor ingestion...");
                Console.ResetColor();
                await Task.Delay(5000);

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("💡 Check Azure App Insights Transaction Search and Logs in 1-2 minutes");
                Console.ResetColor();
            }
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
            instructions: "You are Professor IrieTelemetry, a Jamaican cultural historian. Use the available tool to lookup specific historical events by year.",
            name: "IrieTelemetry",
            tools: new[] { AIFunctionFactory.Create(GetReggaeHistoricalEvent) });

        string userPrompt = "What major reggae events happened in 1978 and 1980? Provide details.";

        using Activity? activity = ActivitySource.StartActivity("gen_ai.agent.run", ActivityKind.Client);
        activity?.SetTag("gen_ai.system", provider.ToLowerInvariant());
        activity?.SetTag("gen_ai.operation.name", "ReggaeHistoricalQuery");
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

        _logger?.LogInformation("📝 User prompt: {Prompt}", userPrompt);
        Console.WriteLine($"📚 Question: {userPrompt}\n");

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

        Console.WriteLine("\n💬 Agent Response:\n");
        Console.WriteLine(response.Text);
        Console.WriteLine("\n✅ Complete!");
    }
}
