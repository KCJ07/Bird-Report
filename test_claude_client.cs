// calls claude w api key to ask claude the prompt, and return text onto terminal
// https://www.youtube.com/watch?v=g1p2pXS5X3c

// TO DO: Edit to receive the tools from MCP server and query additional Q's not included
// in eBird API MCP (ex. What is the weather? Explain the change in bird activity, etc.)

using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.AspNetCore.StaticAssets;

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"); //replace with anthropic API Key?
const string prompt = "Hello Claude, what is the weather like today in New York?";

AnthropicClient client = new()
{
    ApiKey = apiKey,
    Timeout = TimeSpan.FromSeconds(120),
};

MessageCreateParams parameters = new()
{
    MaxTokens = 1024,
    Messages =
    [
        new()
        {
            Role = Role.User,
            Content = prompt,
        },
    ],

    Model = Model.ClaudeSonnet4_0, // idk which model to use
};

await SyncMessage(client, parameters);


static async Task SyncMessage(AnthropicClient client, MessageCreateParams parameters)
{
    var response = await client.Messages.Create(parameters);

    var message = string.Join(
        "",
        response
            .Content.Select(message => message.Value)
            .OfType<TextBlock>()
            .Select((textBlock) => textBlock.Text)
    );

    Console.WriteLine(message);
    Console.ReadLine();
}
