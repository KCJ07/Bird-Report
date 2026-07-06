using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Maui.Devices.Sensors;


var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();

// subject to change currently just asks user for api token
Console.WriteLine("Please input your e-bird API token");
string apiToken = Console.ReadLine();

using HttpClient client = new();
client.DefaultRequestHeaders.Accept.Clear();
// put in each individuals api token
client.DefaultRequestHeaders.Add("X-eBirdApiToken", apiToken);

// get users location via request (up for change to get it done automatically)


// Get Species Count
MainObject result = await client.GetFromJsonAsync<MainObject>
    ("https://www.thecocktaildb.com/api/json/v1/1/list.php?c=list");


// get species (Species count?) from specific locations 
// Get Notable Birds 
// Difference from yesterday
// Checklist volume (to simulate activity)
// Highest checlist volume area