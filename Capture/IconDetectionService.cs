using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace iVillager.Capture;

public class IconDetectionService
{
    private const double MatchThreshold = 0.75;
    private const string IconsFolder = "Assets/Icons";
    private static readonly string[] IconNames = ["v1", "v2", "v3", "v4", "v5", "v6"];

    private readonly Dictionary<string, Mat> _templates = new();
    private string? _sessionLockedIcon;

    public string? SessionLockedIcon => _sessionLockedIcon;

    public void LoadTemplates(string baseDirectory)
    {
        foreach (var name in IconNames)
        {
            var path = Path.Combine(baseDirectory, IconsFolder, $"{name}.png");
            if (!File.Exists(path))
                continue;

            using var bmp = new Bitmap(path);
            using var mat = bmp.ToMat();
            if (mat.IsEmpty)
                continue;

            // Ujednolicamy do grayscale (stabilniejsze matchowanie, szczeg�lnie dla PNG z alpha)
            using var gray = ToGray(mat);
            _templates[name] = gray.Clone();
        }
    }

    /// <summary>
    /// Sprawdza region � je�li wykryje kt�r�� ikon�, zapami�tuje j� na sesj� i zwraca jej nazw�.
    /// </summary>
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

        foreach (var name in toCheck)
        {
            var template = _templates[name];

            if (sourceMat.Width < template.Width || sourceMat.Height < template.Height)
                continue;

            using var result = new Mat();
            CvInvoke.MatchTemplate(sourceMat, template, result, TemplateMatchingType.CcoeffNormed);

            // EmguCV w wielu wersjach wymaga REF (st�d CS1620)
            double minVal = 0.0, maxVal = 0.0;
            Point minLoc = default, maxLoc = default;
            CvInvoke.MinMaxLoc(result, ref minVal, ref maxVal, ref minLoc, ref maxLoc);

            if (maxVal >= MatchThreshold)
            {
                _sessionLockedIcon = name;
                return name;
            }
        }

        return null;
    }

    public void ResetSessionLock()
    {
        _sessionLockedIcon = null;
    }

    private static Mat ToGray(Mat src)
    {
        if (src.NumberOfChannels == 1)
            return src.Clone();

        var gray = new Mat();

        // Najcz�ciej PNG daje 4 kana�y (BGRA), screeny/bitmapy cz�sto 3 (BGR)
        if (src.NumberOfChannels == 4)
            CvInvoke.CvtColor(src, gray, ColorConversion.Bgra2Gray);
        else if (src.NumberOfChannels == 3)
            CvInvoke.CvtColor(src, gray, ColorConversion.Bgr2Gray);
        else
            throw new NotSupportedException($"Unsupported channel count: {src.NumberOfChannels}");

        return gray;
    }
}
