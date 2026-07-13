using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using Amazon.Lambda.Core;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;



/// TODO:
/// make sure all notable stuff also uses distancekm   X 
/// figure out how emailing works
/// test the local db that it works                X     
/// add all users in db (still need to add maria)  X
/// figure out encryption                          X
/// figure out how redirection works for the db


public class Function
{

    public Function()
    {
        
        if (Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME") == null)
        {
            var assemblyDir = Path.GetDirectoryName(typeof(Function).Assembly.Location)!;
            var envPath = Path.Combine(assemblyDir, ".env");
            DotNetEnv.Env.Load(envPath); // only fires when running locally, not in Lambda
        }

    }

    public async Task FunctionHandler(object input, ILambdaContext context)
    {
        using var db = new BirdReportContext();
        List<User> users = await db.Users.ToListAsync(); // list of users

        foreach (var user in users)
        {

            string apiToken = Environment.GetEnvironmentVariable("EBIRD_API_KEY");
            DetQuery Det = new(apiToken);
            int distKm = user.distance; 


            // get users address from table
            var (lat, lng) = await Det.GetLatLongFromAddr(user.Location);

            // Get Species List for past day 
            // might be redundant because new prompt just sends 7 day
            List<Observation> speciesOneDay = await Det.GetSpeciesNearby(lat,lng, distKm);
            string speciesOneDayJSON = JsonSerializer.Serialize(speciesOneDay);
            // Get Species List for past 7 days 
            List<Observation> speciesSevenDay = await Det.GetSpeciesNearby(lat,lng, distKm, 7 ); // floating #s 25 =km 7= prevdays
            string speciesSevenDayJSON = JsonSerializer.Serialize(speciesSevenDay);

            // Get Count of Bird Species from 1 day
            int speciesOneDayCount = speciesOneDay.DistinctBy(o => o.SpeciesCode).Count();

            // Get Count of Bird Species from 7 days 
            int speciesSevenDayCount = speciesSevenDay.DistinctBy(o => o.SpeciesCode).Count();

            // get bird activity for past day
            DateTime yesterday = DateTime.Today.AddDays(-1);
            int year = yesterday.Year;
            int month = yesterday.Month;
            int day = yesterday.Day;

            string countyCode = await Det.GetCountyCode(lat,lng);

            int birdActYesterday = await Det.BirdActViaChecklists(countyCode, year, month, day);

            // Get Notable Birds 
            List<NotableReport> notableReportOneDay = await Det.GetNearbyNotable(countyCode);
            string notableReportOneDayJSON = JsonSerializer.Serialize(notableReportOneDay);

            //
            //  Function to send email using SESv2
            //

            async Task SendEmailAsync(string toAddress, string subject, string body, bool isHtml)
            {
                using var sesClient = new AmazonSimpleEmailServiceV2Client(Amazon.RegionEndpoint.USEast2); 

                var sendRequest = new SendEmailRequest
                {
                    FromEmailAddress = "bird.reporter26@gmail.com", 
                    Destination = new Destination
                    {
                        ToAddresses = new List<string> { toAddress }
                    },
                    Content = new EmailContent
                    {
                        Simple = new Amazon.SimpleEmailV2.Model.Message
                        {
                            Subject = new Amazon.SimpleEmailV2.Model.Content { Data = subject },
                            Body = new Body
                            {
                                Html = isHtml ? new Amazon.SimpleEmailV2.Model.Content { Data = body } : null,
                                Text = !isHtml ? new Amazon.SimpleEmailV2.Model.Content { Data = body } : null,
                            }
                        }
                    }
                };

                try
                {
                    var response = await sesClient.SendEmailAsync(sendRequest);
                    Console.WriteLine($"[SES v2] Sent, MessageId: {response.MessageId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SES v2] Failed to send to {toAddress}: {ex.Message}");
                }
            };

            //// MCP portion

            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            // Daily birding report client:
            //   PART 1 - eBird MCP tools (stdio) for structured observation data
            //   Anthropic's built-in web_search tool for open-ended reasoning
            //   
            //   PART 2 - Combine both into one report
            //
            // NuGet packages needed:
            //   dotnet add package Anthropic
            //   dotnet add package ModelContextProtocol --prerelease
            //   dotnet add package Microsoft.Extensions.AI

            //TODO:
            // move to main file (program.cs)                                   X
            // Fix path                                                         X
            // figure out web searching (doing it at end)                       X
            // combine two outputs into one output                              X
            // make sure it can access env keys                                 X
            // comment code                                                     X
            // figure out how to make it cleanly wrap up at 5 tool call uses    X
            // fix prompts


            var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

            AnthropicClient anthropicClient = new()
            {
                ApiKey = apiKey,
                Timeout = TimeSpan.FromSeconds(120),
            };

            // ============================================================
            // PART 1 - eBird MCP tools, via IChatClient + automatic tool-calling + Web
            // ============================================================


            // assume were in lambda
            var mcpServerDirectory = Path.Combine(Environment.CurrentDirectory, "MCP");

            // Point this at the folder containing server.py + client.py. if were in local
            if (Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME") == null) {
                mcpServerDirectory = Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "..", "Bird-ReportLambda", "MCP");

            } 

            // spawns the MCP server to handle its IO
            // creates a new instance for every user (Currently a lot of overhead but doesn't matter for two people)
            var transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "ebird",
                Command = "python",              // switched this to python
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
                    c.MaximumIterationsPerRequest = 3;  // handles the tool-call loop and sets it to 3. No guarentee that it finishes up cleanly?
                })            
                .Build();


            // calls the LLM with the tools list from our MCP
            async Task<string> AskEbirdAsync(string prompt)
            {
                var relevantToolNames = new HashSet<string> // cuts down token usage by not sending redundant tools
                {
                    "ebird_get_recent_observations_for_species",
                    "ebird_get_nearest_observations_for_species",
                    "ebird_get_historic_observations",
                    "ebird_get_species_list_for_region",
                    "ebird_get_hotspots",
                    "ebird_get_nearby_hotspots",
                    "ebird_get_hotspot_info",
                    "ebird_get_notable_observations",
                    "ebird_get_recent_observations",
                    "ebird_get_regional_statistics_on_date",
                    "ebird_get_recent_checklists_feed",
                };
                var allTools = await mcpClient.ListToolsAsync();
                var filteredTools = allTools.Where(t => relevantToolNames.Contains(t.Name));


                ChatOptions options = new()
                {
                    Tools = [.. filteredTools.Cast<AITool>()], // super fancy syntax
                    MaxOutputTokens = 4096,
                };

                var response = await chatClient.GetResponseAsync(prompt, options);
                Console.WriteLine($"[DEBUG] First response FinishReason: {response.FinishReason}");
                Console.WriteLine($"[DEBUG] First response Text: '{response.Text}'");

                // if this results to true it means did not call as many tools as it wanted
                if (response.FinishReason == ChatFinishReason.ToolCalls)
                {
                    // build the conversation up to this point differentiating our prompt and claudes response
                    var history = new List<ChatMessage>
                    {
                        new(ChatRole.User, prompt),            // prompt
                        new(ChatRole.Assistant, response.Text) // response from claude
                    };

                    history.Add(new ChatMessage(ChatRole.User, "Give your best final answer now based only on what you've already found, without calling any more tools."));

                    response = await chatClient.GetResponseAsync(history, new ChatOptions
                    {
                        MaxOutputTokens = 4096
                    }); // re send everything with no tools

                    Console.WriteLine($"[DEBUG] Second response FinishReason: {response.FinishReason}");
                    Console.WriteLine($"[DEBUG] Second response Text: '{response.Text}'");
                }

                // run a web search on final result to enrich results
                MessageCreateParams parameters = new()
                {
                    Model = "claude-sonnet-5",
                    MaxTokens = 4000,               // need to test this to see if 400 is actually enough
                    Tools =
                    [
                        new WebSearchTool20250305 { MaxUses = 2 }, // claudes current web search engine

                    ],
                    Messages =
                    [
                        new() { Role = Role.User, Content = $"""
                            Here is today's birding digest, generated from eBird observation data:

                            {response.Text}

                            Using web search, verify or add context to the claims in this digest 
                            (e.g. out-of-season sightings, notable rarities, oversummering birds, migration and weaather patterns). Then return 
                            the complete, finalized digest as a single polished piece of writing for a birding 
                            newsletter — do not comment on the task, do not ask clarifying questions, and do not 
                            include any meta-commentary about what you searched for. Just output the final report.
                            """
                        },
                    ],
                };

                
                try
                {
                    var fullResponse = await anthropicClient.Messages.Create(parameters);
                    
                    return string.Join(
                        "",
                        fullResponse.Content
                            .Select(block => block.Value)
                            .OfType<TextBlock>()
                            .Select(textBlock => textBlock.Text)
                    );
                    
                }
                catch (Anthropic.Exceptions.AnthropicBadRequestException ex)
                {
                    Console.WriteLine($"Message: {ex.Message}");
                    Console.WriteLine($"Full details: {ex}");
                    throw; // rethrows crash after logging because program still needs to fail here 
                }



            }



            // ============================================================
            // PART 2 - report
            // ============================================================

                string prompt = $"""
                Using all the following information, enrich it and provide a daily summary
                like you were a small birding digest helper. All the data given are facts and pulled from e-bird so treat it as such.
                Do not add any caveats on your expertise and don't use emojies. Be sure to Talk about birds actually seen and give a few 
                places/ environments you might see a few of them in. Focus more on birding and give a recomendation on where and when to bird the following day because these 
                outputs are from yesterday. You have up to 3 rounds of tool use available if you need to look up additional eBird data — each round can include multiple tool calls if needed. 
                Do not make an absurd amount of calls to ebird because there is a personal limit. 



                Species seen in past 7 days including past day: {speciesSevenDayJSON}
                Past-day species count: {speciesOneDayCount}
                7-day species count: {speciesSevenDayCount}

                County: {countyCode}
                Hotspot activity (checklists submitted yesterday): {birdActYesterday}
                Notable birds in past day: {notableReportOneDayJSON}
                only within this distance except for really notable sightings: {distKm}

                Key questions to answer:
                - Why were these new birds seen yesterday?
                - Why were the rare birds seen, and were they reviewed?
                - Which birds seen here are typically rare for the area?
                - Share a fun fact about one of the birds seen.  
                - Incorporate weather or migration patterns into your summary.
                """;

            var recentObservations = await AskEbirdAsync(prompt);


            Console.WriteLine("=== eBird observations ===");
            Console.WriteLine(recentObservations);


            await SendEmailAsync(user.Email, "Your Daily Birding Digest", recentObservations, isHtml: false);

        }
    }
}