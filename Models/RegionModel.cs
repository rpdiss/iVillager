namespace iVillager.Models;

public class RegionModel
{
    public Dictionary<string, Dictionary<string, RegionEntry>> Groups { get; set; } = new();
}

public class RegionEntry
{
    public required RegionBounds Bounds { get; set; }
    public required string Color { get; set; }
    public string? IconName { get; set; }
}

public class RegionBounds
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}
