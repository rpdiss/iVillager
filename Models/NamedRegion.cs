namespace iVillager.Models;

public class NamedRegion
{
    public string Name { get; set; } = string.Empty;
    public required RegionEntry Value { get; set; }
}
