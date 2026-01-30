using iVillager.Models;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;

namespace iVillager.Overlay;

public static class RegionOverlayRenderer
{
    public static void RenderRegions(
        DrawingContext dc,
        IEnumerable<NamedRegion> regions,
        Rect? previewRect = null,
        string? previewName = null,
        Color? previewColor = null,
        NamedRegion? selectedRegion = null)
    {
        const double HANDLE_SIZE = 10;

        foreach (var region in regions)
        {
            bool isSelected = region == selectedRegion;

            // Kolor tła
            var parsed = ParseColor(region.Value.Color);
            var fillColor = isSelected
                ? Color.FromArgb(60, 255, 140, 0)
                : (parsed ?? Color.FromArgb(60, 0, 255, 0)); // <- tu naprawa nullable

            var brush = new SolidColorBrush(fillColor);

            // Granice
            var bounds = region.Value.Bounds;
            var rect = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);

            // Pen WPF
            var pen = new System.Windows.Media.Pen(
                isSelected ? System.Windows.Media.Brushes.OrangeRed : System.Windows.Media.Brushes.LimeGreen,
                2);

            dc.DrawRectangle(brush, pen, rect);

            // Tekst (FlowDirection WPF)
            var formatted = new FormattedText(
                region.Name,
                CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                16,
                System.Windows.Media.Brushes.Black,
                VisualTreeHelper.GetDpi(Application.Current?.MainWindow ?? new Window()).PixelsPerDip);

            dc.DrawText(formatted, rect.TopLeft + new Vector(6, 6));

            if (isSelected)
            {
                dc.DrawRectangle(
                    System.Windows.Media.Brushes.White,
                    null,
                    new Rect(rect.Right - HANDLE_SIZE, rect.Bottom - HANDLE_SIZE, HANDLE_SIZE, HANDLE_SIZE));
            }
        }

        if (previewRect != null)
        {
            var previewFill = previewColor ?? Color.FromArgb(100, 0, 120, 255);

            var brush = new SolidColorBrush(previewFill);
            var pen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.Orange, 3);

            dc.DrawRectangle(brush, pen, previewRect.Value);

            if (!string.IsNullOrEmpty(previewName))
            {
                var formatted = new FormattedText(
                    previewName,
                    CultureInfo.CurrentCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    16,
                    System.Windows.Media.Brushes.Black,
                    VisualTreeHelper.GetDpi(Application.Current?.MainWindow ?? new Window()).PixelsPerDip);

                dc.DrawText(formatted, previewRect.Value.TopLeft + new Vector(6, 6));
            }
        }
    }

    private static Color? ParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        hex = hex.Trim();
        if (hex.StartsWith("#")) hex = hex[1..];
        if (hex.Length != 6 && hex.Length != 8) return null;
        try
        {
            byte a = 255;
            if (hex.Length == 8)
            {
                a = Convert.ToByte(hex[0..2], 16);
                hex = hex[2..];
            }
            byte r = Convert.ToByte(hex[0..2], 16);
            byte g = Convert.ToByte(hex[2..4], 16);
            byte b = Convert.ToByte(hex[4..6], 16);
            return Color.FromArgb(a, r, g, b);
        }
        catch
        {
            return null;
        }
    }
}
