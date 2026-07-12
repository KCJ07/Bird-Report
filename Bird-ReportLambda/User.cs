public class User
{
    public int Id { get; set; }

    public string Name { get; set; } = "";


    public string Email { get; set; } = "";

    public string Location {get; set; }

    public int distance {get; set; }

    public List<string> Summaries {get; set; } = new();
}
