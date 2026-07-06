// 
// Class to handle Queries to e-Bird API for deterministic calculations
//

using Microsoft.Maui.Devices.Sensors;

class DetQuery
{
    HttpClient client;
    public DetQuery(string apiToken)
    {
        client = new();
        client.DefaultRequestHeaders.Accept.Clear();
        // put in each individuals api token
        client.DefaultRequestHeaders.Add("X-eBirdApiToken", apiToken);

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

            IEnumerable<Location> locations = await Geocoding.Default.GetLocationsAsync(addr);
            Location location = locations.FirstOrDefault();

            return (location.Latitude, location.Longitude);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Console.WriteLine("Address conversion failed please try again");
            }
        }
    }

    // Get a list of species in your local 
    // TODO: need to make a wrapper class and return it as that 
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

}

