#pragma warning disable MEAI001

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
                requestContent.CreateResponse(approved: approved)
            });

            AgentRunResponse finalResponse = await agent.RunAsync(approvalMessage, thread);

            Console.WriteLine(approved
                ? $"\n✅ Approved. Result:\n{finalResponse.Text}"
                : "\n🚫 Tool call denied. Agent continued without executing the function.");
        }
    }
}