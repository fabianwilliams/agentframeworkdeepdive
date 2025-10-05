# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a hands-on lab series for learning the Microsoft Agent Framework (preview), adapted to work with **OpenAI** (direct API) and **local Ollama models** instead of Azure OpenAI. The labs demonstrate building AI agents with various capabilities including multi-turn conversations, function calling, vision, and multi-agent orchestration.

**Target Theme**: All labs use Jamaican/Caribbean history and culture as the subject matter (e.g., "Professor JahMekYanBwoy" - Jamaican history expert).

## Architecture

### Shared Configuration System

The solution uses a **centralized configuration pattern** to avoid duplicating `appsettings.json` across 11 lab projects:

- **`appsettings.json`** (solution root) - Contains API keys and provider settings. **Gitignored for security**.
- **`appsettings.template.json`** (solution root) - Template file checked into git for users to copy.
- **`Shared/AgentConfig.cs`** - Static helper class linked into all lab projects via `.csproj` file reference.

```
AgentFrameworkLabs/
├── appsettings.json              # Ignored by git (user creates from template)
├── appsettings.template.json     # In git - safe template
├── Shared/
│   └── AgentConfig.cs           # Linked file (no namespace)
├── Lab01_SimpleAgent/
│   ├── Program.cs
│   └── Lab01_SimpleAgent.csproj # Links ../Shared/AgentConfig.cs
├── Lab02_ImageAgent/
│   └── ...
└── README.md
```

### Key Design Decisions

1. **No Namespace in AgentConfig.cs**: The shared class has no namespace to avoid import issues across projects.

2. **IChatClient Abstraction**: `AgentConfig` now returns `IChatClient`, wrapping OpenAI's `ChatClient` via `.AsIChatClient()` and constructing `OllamaChatClient` instances so labs can switch providers without touching project code.

3. **File Linking**: Each lab `.csproj` includes:
   ```xml
   <Compile Include="..\Shared\AgentConfig.cs" Link="Shared\AgentConfig.cs" />
   ```

4. **Configuration Loading**: `AgentConfig` looks for `appsettings.json` in parent directory:
   ```csharp
   .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), ".."))
   ```

## Building and Running

### Build Entire Solution
```bash
dotnet build AgentFrameworkLabs.sln
```

### Run a Specific Lab
```bash
cd Lab01_SimpleAgent
dotnet run
```

Or from solution root:
```bash
dotnet run --project Lab01_SimpleAgent/Lab01_SimpleAgent.csproj
```

### Add New Lab Project to Solution
```bash
dotnet new console -n Lab02_NewLab
dotnet sln add Lab02_NewLab/Lab02_NewLab.csproj
```

Then add required packages and link shared config (see "Required NuGet Packages" below).

## Configuration

### Initial Setup
```bash
# Copy template and add your OpenAI API key
cp appsettings.template.json appsettings.json
# Edit appsettings.json and replace YOUR-OPENAI-API-KEY-HERE
```

### Provider Switching
Set `AI:Provider` in `appsettings.json` to either `"OpenAI"` or `"Ollama"`. The shared helper returns the right client for every lab.

- `OpenAI`: ensure `OpenAI:ApiKey` and `OpenAI:Model` are populated (defaults to `gpt-4o-mini`).
- `Ollama`: ensure `ollama serve` is running locally, set `Ollama:Endpoint` (default `http://localhost:11434`) and `Ollama:Model` (e.g., `gpt-oss:120b`, `llama3.3:70b`).

To switch OpenAI models, just update the `OpenAI:Model` value.

## Required NuGet Packages

Each lab project requires these packages:

```xml
<PackageReference Include="Microsoft.Extensions.AI" Version="9.9.1" />
<PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="9.9.0-preview.1.25458.4" />
<PackageReference Include="Microsoft.Extensions.AI.Ollama" Version="9.7.0-preview.1.25356.2" />
<PackageReference Include="Microsoft.Agents.AI" Version="1.0.0-preview.251002.1" />
<PackageReference Include="Microsoft.Agents.AI.OpenAI" Version="1.0.0-preview.251002.1" />
<PackageReference Include="Microsoft.Extensions.Configuration" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="9.0.0" />
<PackageReference Include="OpenAI" Version="2.4.0" />
```

Plus the shared file link:
```xml
<ItemGroup>
  <Compile Include="..\Shared\AgentConfig.cs" Link="Shared\AgentConfig.cs" />
</ItemGroup>
```

## Lab Projects Structure

Each lab follows this pattern:

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Get chat client from shared config
        IChatClient chatClient = AgentConfig.GetChatClient();

        Console.WriteLine($"🤖 Using: {AgentConfig.GetProviderName()}\n");

        // Create agent with themed instructions
        AIAgent agent = new ChatClientAgent(
            chatClient,
            instructions: "You are a [role related to Jamaican/Caribbean culture]...",
            name: "AgentName");

        // Use agent...
        await foreach (var update in agent.RunStreamingAsync(prompt))
        {
            Console.Write(update.Text);
        }
    }
}
```

## Important Files

- **`Shared/AgentConfig.cs`**: Core configuration helper. Modifications here affect all labs.
- **`appsettings.json`**: Contains API keys. **Never commit this file.**
- **`appsettings.template.json`**: Safe template for git. Update when adding new config sections.
- **`.gitignore`**: Ensures `appsettings.json` is never committed.

## Security Notes

- `appsettings.json` is gitignored and must **never** be committed to git
- If accidentally committed, use `git filter-repo --path appsettings.json --invert-paths --force` to remove from history
- After cleaning history, force push: `git push --force origin main`
- The template file (`appsettings.template.json`) is safe to commit and contains placeholder values

## Planned Labs (11 Total)

1. ✅ Simple Agent - Basic agent with streaming
2. Image Analysis - Vision-capable agents
3. Multi-Turn Conversations - Context retention with AgentThread
4. Function Tools - Custom C# functions agents can call
5. Human-in-the-Loop - Approval workflows for function calls
6. Structured Output - JSON schema responses
7. Agent as Tool - Multi-agent composition
8. MCP Server - Model Context Protocol integration
9. OpenTelemetry - Observability and tracing
10. Middleware - Intercepting agent operations
11. Persistence - Saving/resuming conversations

## Development Notes

- **Target Framework**: .NET 9.0
- **Package Versions**: Using preview versions of Microsoft.Agents.AI packages
- **Theme**: All examples use Jamaican/Caribbean history context (Professor JahMekYanBwoy, reggae music, national heroes, etc.)
- **Streaming**: Prefer `RunStreamingAsync()` over `RunAsync()` for better UX
- **Error Handling**: Check `AgentConfig.Configuration` for null/missing keys before using
