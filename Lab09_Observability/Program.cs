using System.ComponentModel;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

internal class Program
{
    private const string SourceName = "AgentFrameworkLabs.Lab09";

    [Description("Get historical events from Jamaica's reggae and sound system era by year.")]
    static string GetReggaeHistoricalEvent(
        [Description("The year to query (e.g., 1950, 1970, 1980)")] int year)
    {
        Console.WriteLine($"🔧 Tool called: GetReggaeHistoricalEvent(year={year})");

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

        return events.TryGetValue(year, out var eventInfo)
            ? eventInfo
            : $"No major documented reggae event for {year}. Try years: {string.Join(", ", events.Keys)}";
    }

    private static async Task Main(string[] args)
    {
        string provider = AgentConfig.Configuration["AI:Provider"] ?? "OpenAI";
        string? connectionString = AgentConfig.Configuration["ConnectionStrings:AZURE_MONITOR_CONNECTION_STRING"];

        // Configure OpenTelemetry TracerProvider
        ResourceBuilder resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName: SourceName, serviceVersion: "1.0.0");

        var tracerBuilder = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddSource(SourceName)
            .AddSource("Microsoft.Extensions.AI")
            .AddSource("Microsoft.Agents.AI")
            .AddConsoleExporter();

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            tracerBuilder.AddAzureMonitorTraceExporter(options =>
                options.ConnectionString = connectionString);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ Azure Monitor trace exporter configured");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠️ Azure Monitor connection string missing; exporting only to console.");
            Console.ResetColor();
        }

        if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            string? apiKey = AgentConfig.Configuration["OpenAI:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                tracerBuilder.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri("https://api.openai.com/v1/observability/traces");
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                    options.Headers =
                        $"Authorization=Bearer {apiKey},OpenAI-Beta=observability=v1";
                    options.ExportProcessorType = ExportProcessorType.Simple;
                });

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✅ OpenAI trace exporter configured");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠️ OpenAI API key missing; skipping OpenAI trace exporter.");
                Console.ResetColor();
            }
        }

        using var tracerProvider = tracerBuilder.Build();
        Console.WriteLine();

        try
        {
            await RunAgentAsync();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n🔄 Flushing telemetry to exporters...");
            Console.ResetColor();

            tracerProvider.ForceFlush();

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⏳ Waiting 5 seconds for Azure Monitor ingestion...");
                Console.ResetColor();
                await Task.Delay(5000);

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("💡 Check Azure App Insights Transaction Search and Logs in 1-2 minutes");
                Console.ResetColor();
            }
        }
        finally
        {
            tracerProvider.Dispose();
        }
    }

    private static async Task RunAgentAsync()
    {
        // Get chat client and enable OpenTelemetry instrumentation
        IChatClient instrumentedChatClient = AgentConfig.GetChatClient()
            .AsBuilder()
            .UseOpenTelemetry(
                sourceName: SourceName,
                configure: options => options.EnableSensitiveData = true)
            .Build();

        Console.WriteLine($"🤖 Using: {AgentConfig.GetProviderName()}\n");

        // Create agent - telemetry is automatically captured through instrumented chat client
        AIAgent agent = new ChatClientAgent(
            instrumentedChatClient,
            instructions: "You are Professor IrieTelemetry, a Jamaican cultural historian. Use the available tool to lookup specific historical events by year.",
            name: "IrieTelemetry",
            tools: new[] { AIFunctionFactory.Create(GetReggaeHistoricalEvent) });

        string userPrompt = "What major reggae events happened in 1978 and 1980? Provide details.";
        Console.WriteLine($"📚 Question: {userPrompt}\n");

        AgentRunResponse response = await agent.RunAsync(userPrompt);

        Console.WriteLine("\n💬 Agent Response:\n");
        Console.WriteLine(response.Text);
        Console.WriteLine("\n✅ Complete!");
    }
}
