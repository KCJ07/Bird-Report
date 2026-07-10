using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;


/// minimal API for local db caching
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();

/// end of minimal API


// subject to change currently just asks user for api token
DetQuery Det = null;
while(true)
{
    try 
    {
        Console.WriteLine("Please input your e-bird API token");
        string apiToken = Console.ReadLine();

        Det = new(apiToken);
        break;
    } 
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        Console.WriteLine("An issue with api token occured please try again");
    }
}





// ask user for address
var (lat, lng) = await Det.GetLatLongFromAddr();

// Get Species List for past day 
List<Observation> speciesOneDay = await Det.GetSpeciesNearby(lat,lng);
string speciesOneDayJSON = JsonSerializer.Serialize(speciesOneDay);
// Get Species List for past 7 days 
List<Observation> speciesSevenDay = await Det.GetSpeciesNearby(lat,lng);
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
