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

3. **Configure API Keys**: Create an `appsettings.json` file (add to .gitignore!):

   **For OpenAI**:
   ```json
   {
     "OpenAI": {
       "ApiKey": "sk-...",
       "Model": "gpt-4o-mini"
     },
     "Ollama": {
       "Endpoint": "http://localhost:11434",
       "Model": "llama3.3:70b"
     }
   }
   ```

4. **Load Configuration**: In your `Program.cs`, add configuration loading:
   ```csharp
   using Microsoft.Extensions.Configuration;

   var configuration = new ConfigurationBuilder()
       .AddJsonFile("appsettings.json", optional: false)
       .Build();
   ```

---

## Lab 1: Create and Run a Simple Agent

**Goal**: Build a basic AI agent using OpenAI or Ollama as the LLM backend.

### Steps

#### Using OpenAI

```csharp
using Microsoft.Extensions.AI;
using OpenAI;

// Configure OpenAI client
var openAiApiKey = configuration["OpenAI:ApiKey"];
var modelName = configuration["OpenAI:Model"] ?? "gpt-4o-mini";

var chatClient = new OpenAIClient(openAiApiKey)
    .GetChatClient(modelName);

// Create AI Agent
AIAgent agent = chatClient.CreateAIAgent(
    instructions: "You are good at telling jokes.",
    name: "Joker");

// Run the agent
string userPrompt = "Tell me a joke about a pirate.";
AgentRunResponse result = await agent.RunAsync(userPrompt);
Console.WriteLine(result.ToString());
```

#### Using Ollama

```csharp
using Microsoft.Extensions.AI;

// Configure Ollama client
var ollamaEndpoint = configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
var modelName = configuration["Ollama:Model"] ?? "llama3.3:70b";

var chatClient = new OllamaChatClient(
    new Uri(ollamaEndpoint),
    modelName);

// Create AI Agent
AIAgent agent = chatClient.CreateAIAgent(
    instructions: "You are good at telling jokes.",
    name: "Joker");

// Run the agent
string userPrompt = "Tell me a joke about a pirate.";
AgentRunResponse result = await agent.RunAsync(userPrompt);
Console.WriteLine(result.ToString());
```

### Streaming Support

For real-time token-by-token output:

```csharp
await foreach (var update in agent.RunStreamingAsync(userPrompt))
{
    Console.Write(update.Text);
}
```

---

## Lab 2: Using Images with an Agent

**Goal**: Send an image URL to the agent for analysis (vision capabilities).

**Note**: For Ollama, use a vision-capable model like `llama3.2-vision:90b`

### Steps

```csharp
// Configure agent for vision
AIAgent agent = chatClient.CreateAIAgent(
    name: "VisionAgent",
    instructions: "You are a helpful agent that can analyze images");

// Create multimodal message
var message = new ChatMessage(
    ChatRole.User,
    new ChatMessageContent[]
    {
        new TextContent("What do you see in this image?"),
        new UriContent("https://upload.wikimedia.org/...", "image/jpeg")
    });

// Send to agent
var response = await agent.RunAsync(message);
Console.WriteLine(response.Text);
```

**For Ollama with llama3.2-vision:90b**:
```csharp
var chatClient = new OllamaChatClient(
    new Uri("http://localhost:11434"),
    "llama3.2-vision:90b");
```

---

## Lab 3: Multi-Turn Conversation with an Agent

**Goal**: Enable multi-turn conversations with context memory.

### Steps

```csharp
// Create agent (OpenAI or Ollama)
AIAgent agent = chatClient.CreateAIAgent(
    instructions: "You are a helpful assistant.");

// Obtain conversation thread
AgentThread thread = agent.GetNewThread();

// Multi-turn dialog
Console.WriteLine(await agent.RunAsync("Tell me a joke about a pirate.", thread));
Console.WriteLine(await agent.RunAsync(
    "Now add some emojis to that joke and tell it in a pirate parrot's voice.",
    thread));
```

The second response will build on the first because we use the same `thread`.

### Multiple Independent Conversations

```csharp
AgentThread thread1 = agent.GetNewThread();
AgentThread thread2 = agent.GetNewThread();

// Separate conversations
Console.WriteLine(await agent.RunAsync("Tell me a joke about a pirate.", thread1));
Console.WriteLine(await agent.RunAsync("Tell me a joke about a robot.", thread2));
```

---

## Lab 4: Using Function Tools with an Agent

**Goal**: Give the agent custom tools/functions to call.

### Steps

```csharp
using System.ComponentModel;

// Define a custom function
[Description("Get the weather for a given location.")]
static string GetWeather(
    [Description("The location to get the weather for.")] string location)
{
    // In production, call a real weather API
    return $"The weather in {location} is cloudy with a high of 15°C.";
}

// Create agent with function tool
AIAgent agent = chatClient.CreateAIAgent(
    instructions: "You are a helpful assistant.",
    tools: new[] { AIFunctionFactory.Create(GetWeather) }
);

// Agent will automatically use the function when needed
Console.WriteLine(await agent.RunAsync("What is the weather like in Amsterdam?"));
```

**Works with both OpenAI and Ollama** (Ollama's function calling support varies by model - `llama3.3:70b` supports it well).

---

## Lab 5: Function Tools with Human-in-the-Loop Approvals

**Goal**: Require user approval before executing sensitive functions.

### Steps

```csharp
// Wrap function with approval requirement
AIFunction weatherFunc = AIFunctionFactory.Create(GetWeather);
AIFunction approvalRequiredWeatherFunc = new ApprovalRequiredAIFunction(weatherFunc);

// Create agent with approval-required tool
AIAgent agent = chatClient.CreateAIAgent(
    instructions: "You are a helpful assistant.",
    tools: new[] { approvalRequiredWeatherFunc }
);

AgentThread thread = agent.GetNewThread();

// First call - will request approval
AgentRunResponse response = await agent.RunAsync("What's the weather like in Amsterdam?", thread);

// Detect approval requests
var approvalRequests = response.Messages
    .SelectMany(msg => msg.Contents)
    .OfType<FunctionApprovalRequestContent>()
    .ToList();

if (approvalRequests.Count > 0)
{
    var requestContent = approvalRequests.First();
    Console.WriteLine($"Approval required for: '{requestContent.FunctionCall.Name}'");

    // Grant approval
    var approvalMessage = new ChatMessage(ChatRole.User, new[] {
        requestContent.CreateResponse(true)
    });

    // Continue with approval
    var finalResponse = await agent.RunAsync(approvalMessage, thread);
    Console.WriteLine(finalResponse.Text);
}
```

---

## Lab 6: Producing Structured Output with Agents

**Goal**: Get JSON output following a specific schema.

### Steps

```csharp
// Define output schema
public class PersonInfo
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("age")] public int? Age { get; set; }
    [JsonPropertyName("occupation")] public string? Occupation { get; set; }
}

// Create JSON schema
JsonElement schema = AIJsonUtilities.CreateJsonSchema(typeof(PersonInfo));

// Configure ChatOptions for structured output
ChatOptions chatOptions = new ChatOptions
{
    ResponseFormat = ChatResponseFormatJson.ForJsonSchema(
        schema: schema,
        schemaName: "PersonInfo",
        schemaDescription: "Information about a person including their name, age, and occupation")
};

// Create agent with structured output
AIAgent agent = chatClient.CreateAIAgent(new ChatClientAgentOptions {
    Name = "HelpfulAssistant",
    Instructions = "You are a helpful assistant.",
    ChatOptions = chatOptions
});

// Query and deserialize
var prompt = "Please provide information about John Smith, who is a 35-year-old software engineer.";
var response = await agent.RunAsync(prompt);

PersonInfo person = response.Deserialize<PersonInfo>(JsonSerializerOptions.Web);
Console.WriteLine($"Name: {person.Name}, Age: {person.Age}, Occupation: {person.Occupation}");
```

**Note**: Structured output works best with OpenAI. Ollama support varies by model.

---

## Lab 7: Using an Agent as a Function Tool

**Goal**: Compose multiple agents - one agent calls another as a tool.

### Steps

```csharp
// Create specialist weather agent
AIAgent weatherAgent = chatClient.CreateAIAgent(
    instructions: "You answer questions about the weather.",
    name: "WeatherAgent",
    description: "An agent that provides weather information.",
    tools: new[] { AIFunctionFactory.Create(GetWeather) }
);

// Create main agent that uses weather agent as a tool
AIAgent mainAgent = chatClient.CreateAIAgent(
    instructions: "You are a helpful assistant who responds in French.",
    tools: new[] { weatherAgent.AsAIFunction() }
);

// Query main agent
Console.WriteLine(await mainAgent.RunAsync("What is the weather like in Amsterdam?"));
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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

// Create agent to expose
AIAgent agent = chatClient.CreateAIAgent(
    instructions: "You are good at telling jokes.",
    name: "Joker");

// Wrap as MCP tool
McpServerTool tool = McpServerTool.Create(agent.AsAIFunction());

// Configure MCP server
var builder = Host.CreateDefaultBuilder();
builder.ConfigureServices(services =>
{
    services.AddMcpServer()
        .WithStdioServerTransport()
        .WithTools(new[] { tool });
});

using IHost host = builder.Build();
await host.RunAsync();
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
using OpenTelemetry;
using OpenTelemetry.Trace;

// Set up tracer
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("agent-telemetry-source")
    .AddConsoleExporter()
    .Build();

// Create base agent
AIAgent baseAgent = chatClient.CreateAIAgent(
    instructions: "You are good at telling jokes.",
    name: "Joker");

// Add telemetry
AIAgent agentWithTelemetry = baseAgent.AsBuilder()
    .UseOpenTelemetry(sourceName: "agent-telemetry-source")
    .Build();

// Use agent - telemetry will be logged
var reply = await agentWithTelemetry.RunAsync("Tell me a joke about a pirate.");
Console.WriteLine(reply.Text);
```

You'll see trace data including:
- Agent name
- Instructions
- Token usage
- Execution time

---

## Lab 10: Adding Middleware to Agents

**Goal**: Intercept and customize agent behavior with middleware.

### Agent-Run Middleware

```csharp
async Task<AgentRunResponse> CustomAgentRunMiddleware(
    IEnumerable<ChatMessage> messages,
    AgentThread? thread,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
{
    Console.WriteLine($"Incoming message count: {messages.Count()}");
    var response = await innerAgent.RunAsync(messages, thread, options, cancellationToken)
        .ConfigureAwait(false);
    Console.WriteLine($"Outgoing message count: {response.Messages.Count}");
    return response;
}

// Attach middleware
var middlewareAgent = baseAgent.AsBuilder()
    .Use(CustomAgentRunMiddleware)
    .Build();
```

### Function-Calling Middleware

```csharp
async ValueTask<object?> CustomFunctionCallingMiddleware(
    AIAgent agent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
{
    Console.WriteLine($"Function Name: {context.Function.Name}");
    var result = await next(context, cancellationToken);
    Console.WriteLine($"Function Call Result: {result}");
    return result;
}

// Attach middleware
middlewareAgent = baseAgent.AsBuilder()
    .Use(CustomFunctionCallingMiddleware)
    .Build();
```

### Chat Client Middleware

```csharp
async Task<ChatResponse> CustomChatClientMiddleware(
    IEnumerable<ChatMessage> messages,
    ChatOptions? options,
    IChatClient innerClient,
    CancellationToken cancellationToken)
{
    Console.WriteLine($"LLM Request: {messages.Count()} message(s)");
    var response = await innerClient.GetResponseAsync(messages, options, cancellationToken);
    Console.WriteLine($"LLM Response: {response.Messages.Count} message(s)");
    return response;
}

// Attach to chat client
var instrumentedChatClient = chatClient.AsBuilder()
    .Use(getResponseFunc: CustomChatClientMiddleware, getStreamingResponseFunc: null)
    .Build();

AIAgent agent = new ChatClientAgent(instrumentedChatClient,
    instructions: "You are a helpful assistant.");
```

---

## Lab 11: Persisting and Resuming Agent Conversations

**Goal**: Save and restore conversation state.

### Steps

```csharp
// Create agent and thread
AIAgent agent = chatClient.CreateAIAgent(
    instructions: "You are a helpful assistant.",
    name: "Assistant");
AgentThread thread = agent.GetNewThread();

// Have a conversation
Console.WriteLine(await agent.RunAsync("Tell me a short pirate joke.", thread));

// Serialize thread
JsonElement serializedThread = thread.Serialize();
string threadJson = JsonSerializer.Serialize(serializedThread, JsonSerializerOptions.Web);

// Save to file
string filePath = Path.Combine(Path.GetTempPath(), "agent_thread.json");
await File.WriteAllTextAsync(filePath, threadJson);

// --- Later, resume conversation ---

// Load from file
string loadedJson = await File.ReadAllTextAsync(filePath);
JsonElement reloaded = JsonSerializer.Deserialize<JsonElement>(loadedJson);

// Deserialize thread
AgentThread resumedThread = agent.DeserializeThread(reloaded);

// Continue conversation
Console.WriteLine(await agent.RunAsync(
    "Now tell that joke in the voice of a parrot.",
    resumedThread));
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
