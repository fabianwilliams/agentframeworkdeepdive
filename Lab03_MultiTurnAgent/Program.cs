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