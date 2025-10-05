# AgentFramework Labs – Codex Notes

## Project Viewpoint

- Multi-project .NET solution (`AgentFrameworkLabs.sln`) with individual labs under `LabXX_*` directories and shared infrastructure in `Shared/`.
- Configuration centralized through `appsettings.json` at the solution root; each lab links `Shared/AgentConfig.cs` to avoid duplication.
- Theme and instructional focus stay on Jamaican/Caribbean history, so agent instructions, sample prompts, and tooling all reinforce that context.

## Provider-Switching Strategy That Worked

| Aspect | Previous Approach (CLAUDE.md) | Current Codex Approach |
|---|---|---|
| Chat client type | Returned concrete `OpenAI.Chat.ChatClient` | Returns `IChatClient` to abstract backend |
| Agent creation | `chatClient.CreateAIAgent(...)` extension | Explicit `new ChatClientAgent(...)` construction |
| Ollama support | Not implemented; docs said “OpenAI only” | `AgentConfig.GetOllamaChatClient` builds `OllamaChatClient` with validation |
| Packages | No Ollama package referenced | Added `Microsoft.Extensions.AI.Ollama` preview package |
| Usage impact | Labs hard-wired to OpenAI, compile errors when swapping clients | Any lab can toggle `AI:Provider` between `OpenAI` and `Ollama` without code edits |

**Key Fix**: By normalising both providers behind `IChatClient`, we can wrap the OpenAI SDK client via `.AsIChatClient()` and instantiate an `OllamaChatClient` directly. Using `ChatClientAgent` ensures the Microsoft Agent Framework consumes the abstraction uniformly, eliminating the type mismatch that blocked Ollama usage.

## Structural Notes

- `Shared/AgentConfig.cs` is the primary extension point; modifications here cascade to every lab.
- Lab projects target `net9.0` and share identical package references with the addition of the Ollama client.
- Streaming interactions (`RunStreamingAsync`) are the default pattern to keep console demos lively.
- Documentation now reflects the provider toggle in both `README.md` and `CLAUDE.md`; this file records the rationale behind the updated pattern.

## Workflow Recommendations

1. Copy `appsettings.template.json` → `appsettings.json`, then set `AI:Provider` to `OpenAI` or `Ollama` as needed.
2. For Ollama: start `ollama serve`, confirm your local model tag (e.g., `gpt-oss:120b`), and ensure the endpoint URL is reachable.
3. Run any lab via `dotnet run --project Lab0X_*/Lab0X_*.csproj`; the shared helper will route to the chosen backend.
4. When extending labs, keep business logic provider-agnostic by staying on `IChatClient` and supplying provider-specific features (structured output, tools, etc.) via `ChatClientAgentOptions`.

## Future Enhancements

- Add guardrails or health checks that ping the configured provider on start-up, surfacing misconfiguration before an agent run.
- Expand docs with a quick decision matrix for which Ollama models support tools, structured output, or vision scenarios.
- Consider embedding test fixtures that exercise both providers to catch integration regressions early.

