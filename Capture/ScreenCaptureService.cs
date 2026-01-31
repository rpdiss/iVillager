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

    private static (double scaleX, double scaleY) GetPrimaryMonitorDpiScale()
    {
        var hMon = MonitorFromPoint(Point.Empty, MONITOR_DEFAULTTOPRIMARY);
        if (hMon == IntPtr.Zero)
            return (1.0, 1.0);
        if (GetDpiForMonitor(hMon, MDT_EFFECTIVE_DPI, out var dpiX, out var dpiY) != 0)
            return (1.0, 1.0);
        return (dpiX / 96.0, dpiY / 96.0);
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(Point pt, int flags);
    private const int MONITOR_DEFAULTTOPRIMARY = 1;

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
    private const int MDT_EFFECTIVE_DPI = 0;

    public Bitmap CaptureRegion(NamedRegion region)
    {
        var (screenX, screenY, _, _) = GetPrimaryScreenBounds();
        var (scaleX, scaleY) = GetPrimaryMonitorDpiScale();
        var b = region.Value.Bounds;

        int x = (int)(screenX + b.X * scaleX);
        int y = (int)(screenY + b.Y * scaleY);
        int w = (int)(b.Width * scaleX);
        int h = (int)(b.Height * scaleY);
        if (w < 1) w = 1;
        if (h < 1) h = 1;

        var captureRect = new Rectangle(x, y, w, h);
        var bmp = new Bitmap(captureRect.Width, captureRect.Height);
        using (var g = Graphics.FromImage(bmp))
            g.CopyFromScreen(captureRect.Location, Point.Empty, captureRect.Size);

        return bmp;
    }
}
