// class for hotspot JSON de-serialization

public class Hotspot
{
    public string LocId { get; set; }
    public string LocName { get; set; }
    public string CountryCode { get; set; }
    public string Subnational1Code { get; set; }
    public string Subnational2Code { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
}