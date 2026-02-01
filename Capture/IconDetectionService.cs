using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace iVillager.Capture;

public class IconDetectionService
{
    private const double MatchThreshold = 0.75;
    private static readonly string[] IconNames = ["v1", "v2", "v3", "v4", "v5", "v6"];

    private readonly Dictionary<string, Mat> _templates = new();
    private string? _sessionLockedIcon;

    public string? SessionLockedIcon => _sessionLockedIcon;

    public void LoadTemplates()
    {
        _templates.Clear();

        if (TryLoadFromFiles(AppContext.BaseDirectory))
        {
            Console.WriteLine($"Loaded {_templates.Count} templates from files");
            return;
        }

        ExtractEmbeddedIcons(AppContext.BaseDirectory);
        TryLoadFromFiles(AppContext.BaseDirectory);

        Console.WriteLine($"Loaded {_templates.Count} templates (from embedded)");
    }

    private bool TryLoadFromFiles(string baseDirectory)
    {
        var iconsFolder = Path.Combine(baseDirectory, "Assets", "Icons");
        if (!Directory.Exists(iconsFolder))
            return false;

        bool anyLoaded = false;

        foreach (var name in IconNames)
        {
            var path = Path.Combine(iconsFolder, $"{name}.png");
            if (!File.Exists(path))
                continue;

            try
            {
                using var bmp = new Bitmap(path);
                using var mat = bmp.ToMat();
                if (mat.IsEmpty)
                    continue;

                using var gray = ToGray(mat);
                _templates[name] = gray.Clone();
                anyLoaded = true;
            }
            catch
            {
                continue;
            }
        }

        return anyLoaded;
    }

    private void ExtractEmbeddedIcons(string baseDirectory)
    {
        var iconsFolder = Path.Combine(baseDirectory, "Assets", "Icons");
        Directory.CreateDirectory(iconsFolder);

        var assembly = Assembly.GetExecutingAssembly();
        var allResources = assembly.GetManifestResourceNames();

        Console.WriteLine("Available resources:");
        foreach (var resource in allResources)
        {
            Console.WriteLine($"  - {resource}");
        }

        foreach (var name in IconNames)
        {
            // Szukaj dok³adnej nazwy zasobu
            var resourceName = $"iVillager.Assets.Icons.{name}.png";
            var matchingResource = allResources.FirstOrDefault(r =>
                r.Equals(resourceName, StringComparison.OrdinalIgnoreCase));

            if (matchingResource == null)
            {
                Console.WriteLine($"Embedded resource not found: {resourceName}");
                continue;
            }

            try
            {
                var targetPath = Path.Combine(iconsFolder, $"{name}.png");

                using var stream = assembly.GetManifestResourceStream(matchingResource);
                if (stream == null)
                {
                    Console.WriteLine($"Stream null for: {matchingResource}");
                    continue;
                }

                using var fileStream = File.Create(targetPath);
                stream.CopyTo(fileStream);

                Console.WriteLine($"Extracted: {name}.png to {targetPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to extract {name}: {ex.Message}");
            }
        }
    }

    public string? DetectInRegion(Bitmap regionBitmap)
    {
        if (regionBitmap.Width < 1 || regionBitmap.Height < 1)
            return null;

        using var sourceMatRaw = regionBitmap.ToMat();
        if (sourceMatRaw.IsEmpty)
            return null;

        using var sourceMat = ToGray(sourceMatRaw);

        var toCheck = _sessionLockedIcon != null && _templates.ContainsKey(_sessionLockedIcon)
            ? new[] { _sessionLockedIcon }
            : IconNames.Where(_templates.ContainsKey).ToArray();

        Console.WriteLine($"Checking icons: {string.Join(", ", toCheck)}");
        Console.WriteLine($"Source image: {sourceMat.Width}x{sourceMat.Height}");

        foreach (var name in toCheck)
        {
            if (!_templates.TryGetValue(name, out var template))
                continue;

            Console.WriteLine($"Template {name}: {template.Width}x{template.Height}");

            if (sourceMat.Width < template.Width || sourceMat.Height < template.Height)
            {
                Console.WriteLine($"Source too small for {name}");
                continue;
            }

            using var result = new Mat();
            CvInvoke.MatchTemplate(sourceMat, template, result, TemplateMatchingType.CcoeffNormed);

            double minVal = 0.0, maxVal = 0.0;
            Point minLoc = default, maxLoc = default;
            CvInvoke.MinMaxLoc(result, ref minVal, ref maxVal, ref minLoc, ref maxLoc);

            Console.WriteLine($"Match {name}: {maxVal:F3}");

            if (maxVal >= MatchThreshold)
            {
                Console.WriteLine($"FOUND: {name} with confidence {maxVal:F3}");
                _sessionLockedIcon = name;
                return name;
            }
        }

        Console.WriteLine("No icon found");
        return null;
    }

    public void ResetSessionLock() => _sessionLockedIcon = null;

    private static Mat ToGray(Mat src)
    {
        if (src.NumberOfChannels == 1)
            return src.Clone();

        var gray = new Mat();

        if (src.NumberOfChannels == 4)
            CvInvoke.CvtColor(src, gray, ColorConversion.Bgra2Gray);
        else if (src.NumberOfChannels == 3)
            CvInvoke.CvtColor(src, gray, ColorConversion.Bgr2Gray);
        else
            throw new NotSupportedException($"Unsupported channel count: {src.NumberOfChannels}");

        return gray;
    }
}
