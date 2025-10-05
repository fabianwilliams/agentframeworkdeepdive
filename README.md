# Microsoft Agent Framework Labs – Step-by-Step Guide

## Adapted for OpenAI and Local Ollama Models

This guide is adapted from the official Microsoft Agent Framework tutorials, specifically configured to work with:
1. **OpenAI** (direct API, not Azure OpenAI)
2. **Local Ollama models** running on your development machine

---

## Setting Up the Project Solution

### Prerequisites

Before diving into the labs, ensure you have:

- **.NET SDK 8.0 or later** – The Agent Framework supports all supported .NET versions (we recommend .NET 8.0+)
- **OpenAI API Key** – Get one from https://platform.openai.com/api-keys
- **Ollama installed locally** – Install from https://ollama.ai
  - Your available models:
    - `gpt-oss:120b` (65 GB)
    - `llama3.2-vision:90b` (54 GB)
    - `llama3.3:70b` (42 GB)
    - `deepseek-r1:70b` (42 GB)
    - `nomic-embed-text:latest` (274 MB)
    - `mxbai-embed-large:latest` (669 MB)
- **Microsoft Agent Framework NuGet packages** – The labs will use the Microsoft Agent Framework libraries

### Required NuGet Packages

For **OpenAI** support:
```bash
dotnet add package Microsoft.Extensions.AI.OpenAI
dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
```

For **Ollama** support:
```bash
dotnet add package Microsoft.Extensions.AI.Ollama
dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
```

### Solution Setup

1. **Create a Solution**: In Visual Studio or via CLI:
   ```bash
   dotnet new sln -n AgentFrameworkLabs
   ```

2. **Add a Console Project per Lab**: For each lab (1 through 11), add a new .NET console application:
   ```bash
   dotnet new console -n Lab01_SimpleAgent
   dotnet sln add Lab01_SimpleAgent/Lab01_SimpleAgent.csproj
   ```

3. **Create Shared Configuration** at solution root:

   Copy the template to create your config file:
   ```bash
   cp appsettings.template.json appsettings.json
   ```

   Then edit `appsettings.json` and add your OpenAI API key:
   ```json
   {
     "AI": {
       "Provider": "OpenAI"
     },
     "OpenAI": {
       "ApiKey": "sk-proj-YOUR-ACTUAL-KEY-HERE",
       "Model": "gpt-4o-mini"
     },
     "Ollama": {
       "Endpoint": "http://localhost:11434",
       "Model": "llama3.3:70b"
     }
   }
   ```

   **Important**: `appsettings.json` is in `.gitignore` to protect your API key. Never commit it to git!

   **To switch providers**: Just change `"AI:Provider"` to either `"OpenAI"` or `"Ollama"` - all labs will automatically use the selected provider!

4. **Create Shared Helper Class** at `Shared/AgentConfig.cs`:
   ```csharp
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
   ```

5. **Required NuGet Packages for Each Lab**: Add to each `.csproj`:
   ```xml
   <ItemGroup>
     <PackageReference Include="Microsoft.Extensions.AI" Version="9.9.1" />
     <PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="9.9.0-preview.1.25458.4" />
     <PackageReference Include="Microsoft.Extensions.AI.Ollama" Version="9.7.0-preview.1.25356.2" />
     <PackageReference Include="Microsoft.Agents.AI" Version="1.0.0-preview.251002.1" />
     <PackageReference Include="Microsoft.Agents.AI.OpenAI" Version="1.0.0-preview.251002.1" />
     <PackageReference Include="Microsoft.Extensions.Configuration" Version="9.0.0" />
     <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="9.0.0" />
     <PackageReference Include="OpenAI" Version="2.4.0" />
   </ItemGroup>

   <ItemGroup>
     <Compile Include="..\Shared\AgentConfig.cs" Link="Shared\AgentConfig.cs" />
   </ItemGroup>
   ```

---

## Lab 1: Create and Run a Simple Agent

**Goal**: Build a basic AI agent that can switch between OpenAI and Ollama via configuration.

### Complete Code

```csharp
using System;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Automatically uses provider from appsettings.json (AI:Provider)
        IChatClient chatClient = AgentConfig.GetChatClient();

        Console.WriteLine($"🤖 Using: {AgentConfig.GetProviderName()}\n");

        // Create AI Agent with Jamaican history expertise
        AIAgent agent = new ChatClientAgent(
            chatClient,
            instructions: "You are a PhD historian specializing in Jamaican history and Caribbean studies. " +
                         "Provide detailed, accurate information with cultural context and sensitivity.",
            name: "Professor JahMekYanBwoy");

        // Run the agent with streaming for real-time response
        string userPrompt = "Tell me about Jamaica's female national heroes and their contributions to the nation.";

        Console.WriteLine($"📚 Question: {userPrompt}\n");
        Console.WriteLine("💬 Response:\n");

        await foreach (var update in agent.RunStreamingAsync(userPrompt))
        {
            Console.Write(update.Text);
        }

        Console.WriteLine("\n\n✅ Complete!");
    }
}
```

### How It Works

- **Shared configuration**: Uses `AgentConfig.GetChatClient()` which reads from shared `appsettings.json`
- **Returns IChatClient**: The helper returns `IChatClient`, enabling the same code to run against OpenAI or Ollama
- **ChatClientAgent**: Agents are created with `ChatClientAgent` so provider-specific clients can be swapped transparently
- **Streaming output**: Uses `RunStreamingAsync()` for real-time token-by-token display
- **Themed prompts**: Agent is configured as a Jamaican history expert

### Key Differences from Original Tutorial

- Works with both OpenAI and Ollama by toggling `AI:Provider`
- Requires the `Microsoft.Extensions.AI.Ollama` package (preview) for local model support
- Uses `ChatClientAgent` directly instead of the older `CreateAIAgent` extension methods

### Try Different Prompts

```csharp
// Jamaican history and culture
"Tell me about Jamaica's female national heroes and their contributions to the nation."
"What was the significance of the Maroon Wars in Jamaica?"
"Explain the cultural impact of Marcus Garvey on Jamaica and the diaspora."
"Describe the development of reggae music and its cultural significance."

// Caribbean studies
"How did the sugar trade shape Caribbean societies?"
"What role did Jamaica play in the broader Caribbean independence movement?"
```

---

## Lab 2: Using Images with an Agent

**Goal**: Analyze images using vision-capable models.

**Note**: For Ollama, change the model in `appsettings.json` to `llama3.2-vision:90b`

### Complete Code

```csharp
using System;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

internal class Program
{
    private static async Task Main(string[] args)
    {
        IChatClient chatClient = AgentConfig.GetChatClient();
        Console.WriteLine($"🤖 Using: {AgentConfig.GetProviderName()}\n");

        // Create vision-capable agent
        AIAgent agent = new ChatClientAgent(
            chatClient,
            name: "CaribbeanArtAnalyst",
            instructions: "You are an art historian specializing in Caribbean and Jamaican visual culture. " +
                         "Analyze imagery with attention to historical and cultural context.");

        // Create multimodal message with image
        ChatMessage message = new ChatMessage(
            ChatRole.User,
            new AIContent[]
            {
                new TextContent(
                    "Analyze this image and describe what you see. " +
                    "If it reflects Caribbean or Jamaican culture, explain the historical context."),
                new UriContent(
                    new Uri("https://upload.wikimedia.org/wikipedia/commons/thumb/1/17/Marcus_Garvey_1924-08-05.jpg/440px-Marcus_Garvey_1924-08-05.jpg"),
                    "image/jpeg")
            });

        Console.WriteLine("🖼️ Analyzing image...\n");
        var response = await agent.RunAsync(new[] { message });
        Console.WriteLine(response.Text);

        Console.WriteLine("\n✅ Complete!");
    }
}
```

### Configuration for Vision Models

**appsettings.json for Ollama vision**:
```json
{
  "AI": {
    "Provider": "Ollama"
  },
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "Model": "llama3.2-vision:90b"
  }
}
```

**For OpenAI**: GPT-4o and GPT-4o-mini support vision natively

---

## Lab 3: Multi-Turn Conversation with an Agent

**Goal**: Enable multi-turn conversations with context memory.

### Complete Code

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

internal class Program
{
    private static async Task Main(string[] args)
    {
        IChatClient chatClient = AgentConfig.GetChatClient();
        Console.WriteLine($"🤖 Using: {AgentConfig.GetProviderName()}\n");

        AIAgent agent = new ChatClientAgent(
            chatClient,
            instructions: "You are a knowledgeable guide on Jamaican music history and culture.",
            name: "MusicHistorian");

        // Create conversation thread to maintain context
        AgentThread thread = agent.GetNewThread();

        // Multi-turn dialog
        Console.WriteLine("💬 Question 1:");
        await foreach (var update in agent.RunStreamingAsync(
            "Who was Bob Marley and what was his impact on reggae music?", thread))
        {
            Console.Write(update.Text);
        }

        Console.WriteLine("\n\n💬 Question 2 (builds on previous context):");
        await foreach (var update in agent.RunStreamingAsync(
            "Tell me more about his Rastafarian beliefs and how they influenced his music.", thread))
        {
            Console.Write(update.Text);
        }

        Console.WriteLine("\n\n✅ Complete!");
    }
}
```

### How Context Works

The second question references "his" without specifying who - the agent remembers we're discussing Bob Marley from the previous exchange because we use the same `thread`.

### Multiple Independent Conversations

```csharp
AgentThread reggaeThread = agent.GetNewThread();
AgentThread danceHallThread = agent.GetNewThread();

// Two separate conversations with independent context
await agent.RunAsync("Tell me about ska music origins.", reggaeThread);
await agent.RunAsync("Explain the rise of dancehall in the 1980s.", danceHallThread);

// Each thread maintains its own conversation history
await agent.RunAsync("Who were the key artists?", reggaeThread); // Refers to ska
await agent.RunAsync("Who were the key artists?", danceHallThread); // Refers to dancehall
```

---

## Lab 4: Using Function Tools with an Agent

**Goal**: Give the agent custom tools/functions to call.

### Complete Code

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.ComponentModel;

internal class Program
{
    [Description("Get information about a Jamaican parish including its capital and key facts.")]
    static string GetParishInfo(
        [Description("The name of the Jamaican parish")] string parishName)
    {
        // Simulated parish database - in production, query a real database
        var parishes = new Dictionary<string, string>
        {
            ["Kingston"] = "Capital: Kingston (also capital of Jamaica). Jamaica's largest city and cultural hub.",
            ["St. Andrew"] = "Capital: Half Way Tree. Part of the Kingston Metropolitan Area.",
            ["Portland"] = "Capital: Port Antonio. Known for its lush vegetation and Blue Lagoon.",
            ["St. Thomas"] = "Capital: Morant Bay. Site of the 1865 Morant Bay Rebellion.",
            ["Westmoreland"] = "Capital: Savanna-la-Mar. Known for its beaches and as birthplace of many reggae artists."
        };

        return parishes.TryGetValue(parishName, out var info)
            ? info
            : $"Parish '{parishName}' not found. Available parishes: {string.Join(", ", parishes.Keys)}";
    }

    private static async Task Main(string[] args)
    {
        IChatClient chatClient = AgentConfig.GetChatClient();
        Console.WriteLine($"🤖 Using: {AgentConfig.GetProviderName()}\n");

        // Create agent with function tool
        AIAgent agent = new ChatClientAgent(
            chatClient,
            instructions: "You are an expert on Jamaican geography and history. Use available tools when needed.",
            name: "GeographyExpert",
            tools: new[] { AIFunctionFactory.Create(GetParishInfo) });

        string userPrompt = "Tell me about the parish where the Morant Bay Rebellion occurred.";
        Console.WriteLine($"📚 Question: {userPrompt}\n");

        await foreach (var update in agent.RunStreamingAsync(userPrompt))
        {
            Console.Write(update.Text);
        }

        Console.WriteLine("\n\n✅ Complete!");
    }
}
```

### How Function Calling Works

1. User asks about the Morant Bay Rebellion location
2. Agent recognizes it needs parish information
3. Agent automatically calls `GetParishInfo("St. Thomas")`
4. Agent incorporates the function result into its response

**Works with both OpenAI and Ollama** (Ollama's function calling support varies by model - `llama3.3:70b` and `deepseek-r1:70b` support it well).

### Collaboration Note

GitHub Copilot spotted that the earlier README sample still used `new ChatClientAgentOptions { ... }`, which causes a runtime failure with `IChatClient`. Copilot suggested switching to the direct `new ChatClientAgent(chatClient, instructions: ..., name: ..., tools: ...)` constructor—the same pattern already in `Lab04_FunctionTools/Program.cs`. The working code above reflects that adjustment so the lab runs cleanly with either provider.

```csharp
// Before (incorrect)
new ChatClientAgent(chatClient, new ChatClientAgentOptions {
    Instructions = "...",
    Tools = new[] { tool }
});

// After (fixed with Copilot's suggestion)
new ChatClientAgent(
    chatClient,
    instructions: "...",
    tools: new[] { tool });
```

---

## Lab 5: Function Tools with Human-in-the-Loop Approvals

**Goal**: Require user approval before executing sensitive functions.

### Complete Code

```csharp
using System;
using System.ComponentModel;
using System.Linq;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

internal class Program
{
    [Description("Provide a quick weather summary for the given city.")]
    private static string GetWeather(
        [Description("City to check")] string city)
    {
        return city.ToLowerInvariant() switch
        {
            "amsterdam" => "Expect light rain with cool breezes off the IJ.",
            "kingston" => "Tropical sunshine with a chance of afternoon showers.",
            _ => $"Weather data for {city} is unavailable—assume warm Caribbean vibes!"
        };
    }

    private static async Task Main(string[] args)
    {
        IChatClient chatClient = AgentConfig.GetChatClient();
        Console.WriteLine($"🤖 Using: {AgentConfig.GetProviderName()}\n");

        AIFunction weatherFunc = AIFunctionFactory.Create(GetWeather);
        AIFunction approvalRequiredWeatherFunc = new ApprovalRequiredAIFunction(weatherFunc);

        AIAgent agent = new ChatClientAgent(
            chatClient,
            instructions: "You are a helpful assistant.",
            tools: new[] { approvalRequiredWeatherFunc });

        AgentThread thread = agent.GetNewThread();

        AgentRunResponse response = await agent.RunAsync(
            "What's the weather like in Amsterdam?",
            thread);

        var approvalRequests = response.Messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionApprovalRequestContent>()
            .ToList();

        if (approvalRequests.Count > 0)
        {
            FunctionApprovalRequestContent requestContent = approvalRequests[0];
            Console.WriteLine($"Approval required for: '{requestContent.FunctionCall.Name}'");

            Console.Write("Approve tool execution? (y/n): ");
            string userInput = Console.ReadLine()?.Trim() ?? string.Empty;
            bool approved = userInput.StartsWith("y", StringComparison.OrdinalIgnoreCase);

            ChatMessage approvalMessage = new ChatMessage(ChatRole.User, new[]
            {
                requestContent.CreateResponse(approve: approved)
            });

            AgentRunResponse finalResponse = await agent.RunAsync(approvalMessage, thread);

            Console.WriteLine(approved
                ? $"\n✅ Approved. Result:\n{finalResponse.Text}"
                : "\n🚫 Tool call denied. Agent continued without executing the function.");
        }
    }
}
```

**Warnings**: `Console.ReadLine()` returns a nullable string. The sample trims with `?? string.Empty` to avoid nullable flow warnings. If analyzers still flag `CS8602`, keep the guard as shown or suppress it explicitly in your project.

---

## Lab 6: Producing Structured Output with Agents

**Goal**: Get JSON output following a specific schema.

### Complete Code

```csharp
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

internal class Program
{
    private static async Task Main(string[] args)
    {
        IChatClient chatClient = AgentConfig.GetChatClient();
        Console.WriteLine($"🤖 Using: {AgentConfig.GetProviderName()}\n");

        JsonElement schema = AIJsonUtilities.CreateJsonSchema(typeof(PersonInfo));

        ChatOptions chatOptions = new ChatOptions
        {
            ResponseFormat = ChatResponseFormatJson.ForJsonSchema(
                schema: schema,
                schemaName: "PersonInfo",
                schemaDescription: "Information about a person including their name, age, and occupation")
        };

        AIAgent agent = new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Name = "HelpfulAssistant",
                Instructions = "You are a helpful assistant.",
                ChatOptions = chatOptions
            });

        string prompt = "Please provide information about John Smith, who is a 35-year-old software engineer.";
        AgentRunResponse response = await agent.RunAsync(prompt);

        PersonInfo person = response.Deserialize<PersonInfo>(JsonSerializerOptions.Web);
        Console.WriteLine($"Name: {person.Name}, Age: {person.Age}, Occupation: {person.Occupation}");
    }
}

internal sealed class PersonInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("age")]
    public int? Age { get; set; }

    [JsonPropertyName("occupation")]
    public string? Occupation { get; set; }
}
```

**Note**: Structured output works best with OpenAI. Ollama support varies by model.

---

## Lab 7: Using an Agent as a Function Tool

**Goal**: Compose multiple agents - one agent calls another as a tool.

### Complete Code

```csharp
using System;
using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

internal class Program
{
    [Description("Provide a concise weather report for the specified city.")]
    private static string GetWeather(
        [Description("City to check")] string city)
    {
        return city.ToLowerInvariant() switch
        {
            "amsterdam" => "Pluie légère et brise fraîche marquent la journée.",
            "kingston" => "Chaleur tropicale avec des averses possibles.",
            _ => $"La météo pour {city} n'est pas disponible, mais l'esprit caribéen reste ensoleillé!"
        };
    }

    private static async Task Main(string[] args)
    {
        IChatClient chatClient = AgentConfig.GetChatClient();
        Console.WriteLine($"🤖 Using: {AgentConfig.GetProviderName()}\n");

        AIAgent weatherAgent = new ChatClientAgent(
            chatClient,
            instructions: "You answer questions about the weather.",
            name: "WeatherAgent",
            tools: new[] { AIFunctionFactory.Create(GetWeather) });

        AIAgent mainAgent = new ChatClientAgent(
            chatClient,
            instructions: "You are a helpful assistant who responds in French.",
            tools: new[] { weatherAgent.AsAIFunction() });

        AgentRunResponse response = await mainAgent.RunAsync("What is the weather like in Amsterdam?");
        Console.WriteLine(response.Text);
    }
}
```

The main agent will:
1. Recognize it needs weather info
2. Call the weatherAgent as a tool
3. Return the result in French (per its instructions)

---

## Lab 8: Exposing an Agent as an MCP Tool

**Goal**: Host an agent as a Model Context Protocol (MCP) server.

### Additional Packages

```bash
dotnet add package Microsoft.Extensions.Hosting
dotnet add package ModelContextProtocol
```

### Steps

```csharp
using System;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

internal class Program
{
    private static async Task Main(string[] args)
    {
        IChatClient chatClient = AgentConfig.GetChatClient();
        Console.WriteLine($"🤖 Using: {AgentConfig.GetProviderName()}\n");

        AIAgent agent = new ChatClientAgent(
            chatClient,
            instructions: "You are good at telling Caribbean-themed jokes.",
            name: "Joker");

        McpServerTool tool = McpServerTool.Create(agent.AsAIFunction());

        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddMcpServer()
                    .WithStdioServerTransport()
                    .WithTools(new[] { tool });
            })
            .Build();

        await host.RunAsync();
    }
}
```

The agent is now available as an MCP tool over STDIO.

---

## Lab 9: Enabling Observability for Agents (OpenTelemetry)

**Goal**: Instrument agents with OpenTelemetry for monitoring.

### Additional Packages

```bash
dotnet add package OpenTelemetry
dotnet add package OpenTelemetry.Exporter.Console
```

### Steps

```csharp
using System;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenTelemetry;
using OpenTelemetry.Trace;

internal class Program
{
    private static async Task Main(string[] args)
    {
        const string sourceName = "agent-telemetry-source";

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .AddConsoleExporter()
            .Build();

        IChatClient chatClient = AgentConfig.GetChatClient();
        Console.WriteLine($"🤖 Using: {AgentConfig.GetProviderName()}\n");

        AIAgent baseAgent = new ChatClientAgent(
            chatClient,
            instructions: "You are good at telling jokes.",
            name: "Joker");

        AIAgent agentWithTelemetry = baseAgent.AsBuilder()
            .UseOpenTelemetry(sourceName: sourceName)
            .Build();

        AgentRunResponse reply = await agentWithTelemetry.RunAsync("Tell me a joke about a pirate.");
        Console.WriteLine(reply.Text);
    }
}
```

You'll see trace data including:
- Agent name
- Instructions
- Token usage
- Execution time

---

## Lab 10: Adding Middleware to Agents

**Goal**: Intercept and customize agent behavior with middleware.

### Complete Code

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

internal class Program
{
    private static async Task Main(string[] args)
    {
        IChatClient chatClient = AgentConfig.GetChatClient();
        Console.WriteLine($"🤖 Using: {AgentConfig.GetProviderName()}\n");

        AIAgent baseAgent = new ChatClientAgent(
            chatClient,
            instructions: "You are a helpful assistant focused on Jamaican music history.",
            name: "MiddlewareDemo");

        AIAgent agentWithRunMiddleware = baseAgent.AsBuilder()
            .Use(CustomAgentRunMiddleware)
            .Build();

        AgentRunResponse runResponse = await agentWithRunMiddleware.RunAsync(
            "Give me a one-sentence history of ska.");
        Console.WriteLine($"Run middleware response: {runResponse.Text}");

        AIAgent agentWithFunctionMiddleware = baseAgent.AsBuilder()
            .Use(CustomFunctionCallingMiddleware)
            .Build();

        // Attach this agent to tools before running to observe function-call logs.
        _ = agentWithFunctionMiddleware;

        IChatClient instrumentedChatClient = chatClient.AsBuilder()
            .Use(getResponseFunc: CustomChatClientMiddleware, getStreamingResponseFunc: null)
            .Build();

        AIAgent agent = new ChatClientAgent(
            instrumentedChatClient,
            instructions: "You are a helpful assistant.");

        AgentRunResponse finalResponse = await agent.RunAsync(
            "Summarize the rise of dancehall music in two sentences.");
        Console.WriteLine($"Chat client middleware response: {finalResponse.Text}");
    }

    private static async Task<AgentRunResponse> CustomAgentRunMiddleware(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Incoming message count: {messages.Count()}");
        AgentRunResponse response = await innerAgent.RunAsync(messages, thread, options, cancellationToken);
        Console.WriteLine($"Outgoing message count: {response.Messages.Count}");
        return response;
    }

    private static async ValueTask<object?> CustomFunctionCallingMiddleware(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Function Name: {context.Function.Name}");
        object? result = await next(context, cancellationToken);
        Console.WriteLine($"Function Call Result: {result}");
        return result;
    }

    private static async Task<ChatResponse> CustomChatClientMiddleware(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        IChatClient innerClient,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"LLM Request: {messages.Count()} message(s)");
        ChatResponse response = await innerClient.GetResponseAsync(messages, options, cancellationToken);
        Console.WriteLine($"LLM Response: {response.Messages.Count} message(s)");
        return response;
    }
}
```

---

## Lab 11: Persisting and Resuming Agent Conversations

**Goal**: Save and restore conversation state.

### Complete Code

```csharp
using System;
using System.IO;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

internal class Program
{
    private static async Task Main(string[] args)
    {
        IChatClient chatClient = AgentConfig.GetChatClient();
        Console.WriteLine($"🤖 Using: {AgentConfig.GetProviderName()}\n");

        AIAgent agent = new ChatClientAgent(
            chatClient,
            instructions: "You are a helpful assistant.",
            name: "Assistant");

        AgentThread thread = agent.GetNewThread();

        AgentRunResponse initialResponse = await agent.RunAsync(
            "Tell me a short pirate joke.",
            thread);
        Console.WriteLine(initialResponse.Text);

        JsonElement serializedThread = thread.Serialize();
        string filePath = Path.Combine(Path.GetTempPath(), "agent_thread.json");
        await File.WriteAllTextAsync(
            filePath,
            JsonSerializer.Serialize(serializedThread, JsonSerializerOptions.Web));

        string loadedJson = await File.ReadAllTextAsync(filePath);
        JsonElement reloaded = JsonSerializer.Deserialize<JsonElement>(loadedJson);

        AgentThread resumedThread = agent.DeserializeThread(reloaded);

        AgentRunResponse followUp = await agent.RunAsync(
            "Now tell that joke in the voice of a parrot.",
            resumedThread);
        Console.WriteLine(followUp.Text);
    }
}
```

The agent remembers the previous joke context!

---

## Ollama-Specific Considerations

### Model Selection

Different Ollama models have different capabilities:

| Model | Size | Best For | Function Calling | Vision |
|-------|------|----------|------------------|--------|
| `llama3.3:70b` | 42 GB | General chat, function calling | ✅ | ❌ |
| `llama3.2-vision:90b` | 54 GB | Image analysis | ⚠️ Limited | ✅ |
| `deepseek-r1:70b` | 42 GB | Reasoning tasks | ✅ | ❌ |
| `gpt-oss:120b` | 65 GB | Complex tasks | ✅ | ❌ |

### Performance Tips

1. **Ensure Ollama is running**: `ollama serve`
2. **Pre-load models**: `ollama run llama3.3:70b`
3. **Monitor resources**: Large models need significant RAM/VRAM
4. **Adjust context window** in ChatOptions if needed:
   ```csharp
   var chatOptions = new ChatOptions
   {
       MaxTokens = 4096
   };
   ```

### Embedding Models

For RAG (Retrieval-Augmented Generation) use your embedding models:

```csharp
var embeddingClient = new OllamaEmbeddingClient(
    new Uri("http://localhost:11434"),
    "nomic-embed-text:latest");
```

---

## OpenAI vs Ollama Quick Reference

### OpenAI Client Setup
```csharp
using OpenAI;

var chatClient = new OpenAIClient(apiKey)
    .GetChatClient("gpt-4o-mini");
```

### Ollama Client Setup
```csharp
using Microsoft.Extensions.AI;

var chatClient = new OllamaChatClient(
    new Uri("http://localhost:11434"),
    "llama3.3:70b");
```

### Switching Between Providers

Use configuration to easily switch:

```csharp
var provider = configuration["AI:Provider"]; // "OpenAI" or "Ollama"

IChatClient chatClient = provider switch
{
    "OpenAI" => new OpenAIClient(configuration["OpenAI:ApiKey"])
        .GetChatClient(configuration["OpenAI:Model"]),
    "Ollama" => new OllamaChatClient(
        new Uri(configuration["Ollama:Endpoint"]),
        configuration["Ollama:Model"]),
    _ => throw new InvalidOperationException($"Unknown provider: {provider}")
};
```

---

## Troubleshooting

### Ollama Connection Issues

```bash
# Check if Ollama is running
curl http://localhost:11434/api/tags

# View loaded models
ollama list

# Run a model manually
ollama run llama3.3:70b
```

### OpenAI Rate Limits

- Free tier: 3 requests/minute
- Tier 1: 500 requests/minute
- Consider caching responses
- Use streaming for better UX

### Memory Issues with Large Ollama Models

- `gpt-oss:120b` needs ~65 GB RAM
- Consider using smaller models for development
- Use `llama3.3:70b` (42 GB) for a good balance

---

## Additional Resources

- [Microsoft Agent Framework Docs](https://learn.microsoft.com/en-us/agent-framework/)
- [Microsoft.Extensions.AI Documentation](https://learn.microsoft.com/en-us/dotnet/ai/get-started/dotnet-ai-overview)
- [OpenAI API Reference](https://platform.openai.com/docs/api-reference)
- [Ollama Documentation](https://github.com/ollama/ollama/blob/main/docs/api.md)
- [Model Context Protocol (MCP)](https://modelcontextprotocol.io/)

---

## Next Steps

1. Complete all 11 labs
2. Experiment with different models (OpenAI vs Ollama)
3. Build your own custom agents
4. Integrate with your applications
5. Explore advanced scenarios:
   - Multi-agent orchestration
   - RAG with embedding models
   - Production deployment patterns
   - Monitoring and observability

Happy coding! 🚀
