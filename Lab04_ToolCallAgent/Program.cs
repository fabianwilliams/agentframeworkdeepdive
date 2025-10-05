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