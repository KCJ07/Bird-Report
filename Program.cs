using System.Net.Http.Headers;
using System.Net.Http.Json;



var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();

// subject to change currently just asks user for api token
Console.WriteLine("Please input your e-bird API token");
string apiToken = Console.ReadLine();

// Get Species Count

// get species (Species count?) from specific locations 
// Get Notable Birds 
// Difference from yesterday
// Checklist volume (to simulate activity)
// Highest checlist volume area