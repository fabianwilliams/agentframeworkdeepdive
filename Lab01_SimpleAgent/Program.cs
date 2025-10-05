using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Automatically uses provider from appsettings.json (AI:Provider)
        // Switch between "OpenAI" and "Ollama" by changing that setting
        ChatClient chatClient = AgentConfig.GetChatClient();

        Console.WriteLine($"🤖 Using: {AgentConfig.GetProviderName()}\n");

        // Create AI Agent with Jamaican history expertise
        AIAgent agent = chatClient.CreateAIAgent(
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