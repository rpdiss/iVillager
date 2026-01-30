using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using iVillager.Models;

namespace iVillager.Capture;

public class RegionConfigManager(string path)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public Dictionary<string, Dictionary<string, RegionEntry>> LoadAll()
    {
        if (!File.Exists(path))
            return new();

        var json = File.ReadAllText(path);
        var result = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, RegionEntry>>>(json, Options);
        return result ?? new();
    }

    public void SaveGroup(string groupId, IEnumerable<NamedRegion> regions)
    {
        var all = LoadAll();
        all[groupId] = regions.ToDictionary(r => r.Name, r => r.Value);

        var json = JsonSerializer.Serialize(all, Options);
        File.WriteAllText(path, json);
    }

    public List<NamedRegion> LoadGroup(string groupId)
    {
        var all = LoadAll();
        if (!all.TryGetValue(groupId, out var group))
            return new();

        return group.Select(pair =>
        {
            var entry = pair.Value;
            var b = entry.Bounds ?? new RegionBounds();
            return new NamedRegion
            {
                Name = pair.Key,
                Value = new RegionEntry
                {
                    Color = entry.Color ?? "#00ff00",
                    IconName = entry.IconName,
                    Bounds = new RegionBounds
                    {
                        X = b.X,
                        Y = b.Y,
                        Width = b.Width,
                        Height = b.Height
                    }
                }
            };
        }).ToList();
    }
}
