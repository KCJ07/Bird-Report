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

    [JsonPropertyName("locName")]
    public string LocName { get; set; }

    [JsonPropertyName("obsDt")]
    public string ObsDt { get; set; }   

    [JsonPropertyName("howMany")]
    public int? HowMany { get; set; }   // nullable, count is sometimes omitted

    [JsonPropertyName("obsValid")]
    public bool ObsValid { get; set; }

    [JsonPropertyName("obsReviewed")]
    public bool ObsReviewed { get; set; }

    [JsonPropertyName("locationPrivate")]
    public bool LocationPrivate { get; set; }

    [JsonPropertyName("exoticCategory")]
    public string? ExoticCategory { get; set; }
}

