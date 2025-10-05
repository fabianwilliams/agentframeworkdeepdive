using System;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

internal class Program
{
    private static async Task Main(string[] args)
    {
        IChatClient chatClient = AgentConfig.GetChatClient();
        Console.WriteLine($"🤖 Using: {AgentConfig.GetProviderName()}\n");

        // Configure a vision-capable agent themed around Caribbean art history
        AIAgent agent = new ChatClientAgent(
            chatClient,
            name: "CaribbeanArtAnalyst",
            instructions: "You are an art historian specializing in Caribbean and Jamaican visual culture. " +
                         "Analyze imagery with attention to historical and cultural context.");

        ChatMessage message = new ChatMessage(
            ChatRole.User,
            new AIContent[]
            {
                new TextContent(
                    "Analyze this image and describe what you see. " +
                    "If it reflects Caribbean or Jamaican culture, explain the historical context."),
                new UriContent(
                    new Uri("https://upload.wikimedia.org/wikipedia/commons/2/21/Garvey_Statue.jpg"),
                    "image/jpeg")
            });

        Console.WriteLine("🖼️ Analyzing image...\n");
        AgentRunResponse response = await agent.RunAsync(new[] { message });
        Console.WriteLine(response.Text);

        Console.WriteLine("\n✅ Complete!");
    }
}
