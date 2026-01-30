using System.Drawing;
using System.Runtime.InteropServices;
using iVillager.Models;
using Point = System.Drawing.Point;

namespace iVillager.Capture;

public class ScreenCaptureService
{
    private static (int X, int Y, int Width, int Height) GetPrimaryScreenBounds()
    {
        const int SM_CXSCREEN = 0;
        const int SM_CYSCREEN = 1;
        return (0, 0, GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN));
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    public Bitmap CaptureRegion(NamedRegion region)
    {
        var (screenX, screenY, screenW, screenH) = GetPrimaryScreenBounds();
        var bounds = region.Value.Bounds;

        var captureRect = new Rectangle(
            (int)(screenX + bounds.X),
            (int)(screenY + bounds.Y),
            (int)bounds.Width,
            (int)bounds.Height);

        var bmp = new Bitmap(captureRect.Width, captureRect.Height);
        using (var g = Graphics.FromImage(bmp))
            g.CopyFromScreen(captureRect.Location, Point.Empty, captureRect.Size);

        return bmp;
    }
}
