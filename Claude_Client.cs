
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
// Daily birding report client:
//   PART 1 - eBird MCP tools (stdio) for structured observation data
//   PART 2 - Anthropic's built-in web_search tool for open-ended reasoning
//            (e.g. "why an influx of X", "what's the weather in Y")
//   PART 3 - Combine both into one report
//
// NuGet packages needed:
//   dotnet add package Anthropic
//   dotnet add package ModelContextProtocol --prerelease
//   dotnet add package Microsoft.Extensions.AI

//TODO:
// move to main file (program.cs)
// Fix path                                 X
// figure out web searching
// combine two outputs into one output
// make sure it can access env keys
// comment code 
// figure out how to make it cleanly wrap up at 5 tool call uses


using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

AnthropicClient anthropicClient = new()
{
    ApiKey = apiKey,
    Timeout = TimeSpan.FromSeconds(120),
};

// ============================================================
// PART 1 - eBird MCP tools, via IChatClient + automatic tool-calling
// ============================================================

// Point this at the folder containing server.py + client.py.
var mcpServerDirectory = Path.Combine(Environment.CurrentDirectory + "/MCP", "mcp-server");

// spawns the MCP server to handle its IO
var transport = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "ebird",
    Command = "python3",              // switched this to python3 because client and server in mcp are py3. pretty sure this means we need to install py3 to run this as well
    Arguments = ["server.py"],
    WorkingDirectory = mcpServerDirectory,
    EnvironmentVariables = new Dictionary<string, string?>
    {
        ["EBIRD_API_KEY"] = Environment.GetEnvironmentVariable("EBIRD_API_KEY"),
    },
});

await using var mcpClient = await McpClient.CreateAsync(transport);

IChatClient innerChatClient = anthropicClient
    .AsIChatClient("claude-sonnet-5");   // current Sonnet model id

IChatClient chatClient = innerChatClient
    .AsBuilder()
    .UseFunctionInvocation(configure: c =>
    {
        c.MaximumIterationsPerRequest = 5;  // handles the tool-call loop and sets it to 5. No guarentee that it finishes up cleanly?
    })            
    .Build();


// calls the LLM with the tools list from our MCP
async Task<string> AskEbirdAsync(string prompt)
{
    ChatOptions options = new()
    {
        Tools = [.. (await mcpClient.ListToolsAsync()).Cast<AITool>()], // super fancy syntax
    };

    var response = await chatClient.GetResponseAsync(prompt, options);

    // if this results to true it means did not call as many tools as it wanted
    if (response.FinishReason == ChatFinishReason.ToolCalls)
    {
        // build the conversation up to this point differentiating our prompt and claudes response
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, prompt),            // prompt
            new(ChatRole.Assistant, response.Text) // response
        };

        history.Add(new ChatMessage(ChatRole.User, "Give your best final answer now based only on what you've already found, without calling any more tools."));

        response = await chatClient.GetResponseAsync(history, new ChatOptions()); // re send everything with no tools

    }

    return response.Text;
}

// ============================================================
// PART 2 - Weather / open-ended reasoning, via the raw Messages API
// with Anthropic's built-in web_search server tool. No MCP client,
// no local handler - Claude calls it and returns cited results.
// ============================================================

async Task<string> AskWithWebSearchAsync(string prompt)
{
    MessageCreateParams parameters = new()
    {
        Model = "claude-sonnet-5",
        MaxTokens = 2048,
        Tools =
        [
            // NOTE: Anthropic periodically ships new dated versions of this
            // tool (e.g. WebSearchTool20250305, newer dynamic-filtering
            // versions followed). Check IntelliSense / the current C# SDK
            // docs for the class name that matches your installed package
            // version if this doesn't compile as-is.
            new WebSearchTool20250305(),
        ],
        Messages =
        [
            new() { Role = Role.User, Content = prompt },
        ],
    };

    var response = await anthropicClient.Messages.Create(parameters);

    return string.Join(
        "",
        response.Content
            .Select(block => block.Value)
            .OfType<TextBlock>()
            .Select(textBlock => textBlock.Text)
    );
}

// ============================================================
// PART 3 - Compile the daily report
// ============================================================

var recentObservations = await AskEbirdAsync(
    "What are today's notable bird observations in Arkansas (region code US-AR)?");

var reasoning = await AskWithWebSearchAsync(
    "Search for reasons there might be an influx of mountain bluebirds in Arkansas " +
    "recently, and give today's weather forecast for northwest Arkansas.");

Console.WriteLine("=== eBird observations ===");
Console.WriteLine(recentObservations);
Console.WriteLine();
Console.WriteLine("=== Weather & context ===");
Console.WriteLine(reasoning);
Console.ReadLine();