// wrapper class for individual e-bird observations for JSON de-serialization

using System.Text.Json.Serialization;

public class Observation
{
    [JsonPropertyName("speciesCode")]
    public string SpeciesCode { get; set; }

    [JsonPropertyName("comName")]
    public string ComName { get; set; }

    [JsonPropertyName("sciName")]
    public string SciName { get; set; }

    [JsonPropertyName("locId")]
    public string LocId { get; set; }

    [JsonPropertyName("locName")]
    public string LocName { get; set; }

    [JsonPropertyName("obsDt")]
    public string ObsDt { get; set; }   // eBird sends this as a string, e.g. "2026-07-05 08:30"

    [JsonPropertyName("howMany")]
    public int? HowMany { get; set; }   // nullable — see earlier conversation, count is sometimes omitted

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }

    [JsonPropertyName("obsValid")]
    public bool ObsValid { get; set; }

    [JsonPropertyName("obsReviewed")]
    public bool ObsReviewed { get; set; }

    [JsonPropertyName("locationPrivate")]
    public bool LocationPrivate { get; set; }

    [JsonPropertyName("subId")]
    public string SubId { get; set; }
}