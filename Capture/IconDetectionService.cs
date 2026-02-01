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
    private const string ResourcePrefix = "iVillager.Assets.Icons.";

    private readonly Dictionary<string, Mat> _templates = new();
    private string? _sessionLockedIcon;
    private readonly Assembly _assembly;

    public string? SessionLockedIcon => _sessionLockedIcon;

    public IconDetectionService()
    {
        _assembly = Assembly.GetExecutingAssembly();
    }

    public void LoadTemplates()
    {
        _templates.Clear();

        foreach (var name in IconNames)
        {
            var resourceName = $"{ResourcePrefix}{name}.png";
            try
            {
                using var stream = _assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    Console.WriteLine($"Embedded resource not found: {resourceName}");
                    continue;
                }

                using var image = Image.FromStream(stream);
                using var bmp = new Bitmap(image);
                using var mat = bmp.ToMat();

                if (mat.IsEmpty)
                {
                    Console.WriteLine($"Empty mat for: {name}");
                    continue;
                }

                using var gray = ToGray(mat);
                _templates[name] = gray.Clone();
                Console.WriteLine($"Loaded template: {name} ({mat.Width}x{mat.Height})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load icon {name}: {ex.Message}");
            }
        }

        Console.WriteLine($"Total loaded templates: {_templates.Count}");
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

        if (toCheck.Length == 0)
        {
            Console.WriteLine("No templates loaded to check against");
            return null;
        }

        Console.WriteLine($"Checking {toCheck.Length} icons");
        Console.WriteLine($"Source: {sourceMat.Width}x{sourceMat.Height}");

        string? bestMatch = null;
        double bestConfidence = 0;

        foreach (var name in toCheck)
        {
            if (!_templates.TryGetValue(name, out var template))
                continue;

            if (sourceMat.Width < template.Width || sourceMat.Height < template.Height)
            {
                Console.WriteLine($"Source too small for {name} ({template.Width}x{template.Height})");
                continue;
            }

            using var result = new Mat();
            CvInvoke.MatchTemplate(sourceMat, template, result, TemplateMatchingType.CcoeffNormed);

            double minVal = 0.0, maxVal = 0.0;
            Point minLoc = default, maxLoc = default;
            CvInvoke.MinMaxLoc(result, ref minVal, ref maxVal, ref minLoc, ref maxLoc);

            Console.WriteLine($"  {name}: {maxVal:F3}");

            if (maxVal >= MatchThreshold && maxVal > bestConfidence)
            {
                bestConfidence = maxVal;
                bestMatch = name;
            }
        }

        if (bestMatch != null)
        {
            Console.WriteLine($"FOUND: {bestMatch} with confidence {bestConfidence:F3}");
            _sessionLockedIcon = bestMatch;
            return bestMatch;
        }

        Console.WriteLine("No icon found above threshold");
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

    // Metoda pomocnicza do debugowania zasobów
    public void DebugResources()
    {
        Console.WriteLine("=== Embedded Resources ===");
        var resources = _assembly.GetManifestResourceNames();
        foreach (var resource in resources)
        {
            Console.WriteLine($"  {resource}");
        }
        Console.WriteLine("==========================");
    }
}