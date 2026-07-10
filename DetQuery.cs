// 
// Class to handle Queries to e-Bird API for deterministic calculations
//

using System.Text;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Maui.Devices.Sensors;
using System.Text.Json;


class DetQuery
{
    HttpClient client;
    public DetQuery(string apiToken)
    {
        client = new();
        client.DefaultRequestHeaders.Accept.Clear();
        // put in each individuals api token
        client.DefaultRequestHeaders.Add("x-ebirdapitoken", apiToken);

    }

    // get users location via request (up for change to get it done automatically)
    public async Task<(double lat, double lng)> GetLatLongFromAddr()
    {

        // loop untill valid address is recieved
        while(true)
        {
            try {
            Console.WriteLine("Please put in your address");
            string addr = Console.ReadLine();

            string url = "https://geocoding.geo.census.gov/geocoder/locations/onelineaddress" +
                $"?address={Uri.EscapeDataString(addr)}" +
                "&benchmark=Public_AR_Current&format=json";

            using var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode(); 

            using var stream = await response.Content.ReadAsStreamAsync(); 
            using var doc = await JsonDocument.ParseAsync(stream);

            var matches =doc.RootElement
            .GetProperty("result")
            .GetProperty("addressMatches");

            if (matches.GetArrayLength() == 0)
                throw new InvalidOperationException("No matches found for that address.");

            // gets the first address that matches
            var coords = matches[0].GetProperty("coordinates");

            double lng = coords.GetProperty("x").GetDouble();
            double lat = coords.GetProperty("y").GetDouble();

            return (lat, lng);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine("Address conversion failed please try again");
            }
        }
    }

    // Get a list of species in your local 
    public async Task<List<Observation>> GetSpeciesNearby(double lat, double lng, int distKm = 25, int prevDays = 1)
    {
        // keeps distance between apis abilities
        distKm = Math.Clamp(distKm, 0, 50);
  
        string url = $"https://api.ebird.org/v2/data/obs/geo/recent" +
                    $"?lat={lat:F2}" +
                    $"&lng={lng:F2}" +
                    $"&dist={distKm}" +
                    $"&back={prevDays}";

        try
        {
        List<Observation> observations = await client.GetFromJsonAsync<List<Observation>>(url);
                return observations ?? new List<Observation>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not query eBird nearby API: {ex.Message}");

            return new List<Observation>();
        }

    }

    // get local county code 
    public async Task<string> GetCountyCode(double lat, double lng)
    {
        List<Hotspot> hotspots = new(); // might break everything

        try
        {
        string url = "https://api.ebird.org/v2/ref/hotspot/geo" + 
                    $"?lat={lat:F2}" +
                    $"&lng={lng:F2}" +
                    "&fmt=json";


        hotspots = await client.GetFromJsonAsync<List<Hotspot>>(url);
        
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine("error in getting nearest hotspot for county code");
        }

        Hotspot hotspot = hotspots.FirstOrDefault() ?? throw new InvalidOperationException("not able to find a hotspot for county code");

        return hotspot.Subnational2Code;
    }

    // gets the number of nearby checklists to a given region on a specific day
    public async Task<List<Hotspot>>GetChecklistsByDay(string regionCode, int y, int m, int d)
    {
        List<Hotspot> hotspots = new(); // might break everything

        try
        {
            string url = $"https://api.ebird.org/v2/product/lists/{regionCode}/{y}/{m}/{d}?maxResults=200";


            hotspots = await client.GetFromJsonAsync<List<Hotspot>>(url);

        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine("error in getting nearest hotspots using county code");
        }

 
        return hotspots;
    }

    // calculates birding activity via the number of nearby checkpoints MAX is 200
    public async Task<int>BirdActViaChecklists(string regionCode, int y, int m, int d)
    {
        List<Hotspot> hotspots = await GetChecklistsByDay(regionCode, y, m, d);
        return hotspots.Count();
    }

    // Gets notable observations of a bird
    public async Task<List<NotableReport>>GetNearbyNotable(string regionCode, int prevDays = 1, string detail = "full")
    {
        List<Observation> observations = new();
        string url = $"https://api.ebird.org/v2/data/obs/{regionCode}/recent/notable" +
            $"?back={prevDays}" +
            $"&detail={detail}";

        try
        {
        observations = await client.GetFromJsonAsync<List<Observation>>(url);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine("Issue querying notbale ebird API");
        }

        List<NotableReport> report = observations.Select(o => new NotableReport
        {
            ComName = o.ComName,
            SciName = o.SciName,
            LocName = o.LocName,
            ObsDt = o.ObsDt,
            HowMany = o.HowMany,
            ObsReviewed = o.ObsReviewed,
            LocationPrivate = o.LocationPrivate
        }).ToList();

        return report;
    }
}

