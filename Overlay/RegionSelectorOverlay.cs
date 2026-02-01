using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using iVillager.Capture;
using iVillager.Models;

namespace iVillager.Overlay;

public class RegionSelectorOverlay : Window
{
    private Point? _startPoint;
    private Rect? _previewRect;
    private string? _previewName;
    private Color? _previewColor;
    private readonly string _groupId;

    private NamedRegion? _selectedRegion;
    private Point? _dragStart;
    private Rect? _originalRect;
    private bool _isDraggingRegion = false;
    private bool _isResizing = false;
    private const double HANDLE_SIZE = 10.0;

    private readonly List<NamedRegion> _regions = new();

    // JSON zamiast YAML
    private readonly RegionConfigManager _configManager = new("region_config.json");

    private Button? _saveButton;

    // 🔧 Ignoruj pierwszy Ctrl+PageUp, który otworzył overlay
    private bool _ignoreFirstCtrlPageUp = true;

    public RegionSelectorOverlay(string groupId = "v1")
    {
        _groupId = groupId;

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0));
        ShowInTaskbar = false;
        WindowState = WindowState.Maximized;

        Focusable = true;
        AllowsTransparency = true;

        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;

        Activated += (_, _) =>
        {
            Focus();
            Keyboard.Focus(this);
        };

        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;

        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;

        KeyDown += OnKeyDown;

        var loaded = _configManager.LoadGroup(_groupId);
        if (loaded.Count > 0)
            _regions.AddRange(loaded);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_FRAMECHANGED);
    }

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_FRAMECHANGED = 0x0020;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var canvas = new Canvas();
        Content = canvas;

        _saveButton = new Button
        {
            Content = "Zapisz",
            Width = 240,
            Height = 72,
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Background = new SolidColorBrush(Color.FromArgb(220, 0, 120, 215)),
            Foreground = Brushes.White,
            BorderBrush = Brushes.DarkSlateGray,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand
        };

        _saveButton.Click += (_, _) => SaveAndClose();

        canvas.Children.Add(_saveButton);
        UpdateSaveButtonPosition();

        // 🔧 Ignorujemy Ctrl+PageUp z tego samego “wciśnięcia” co otwarcie overlay
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _ignoreFirstCtrlPageUp = false;
        }), DispatcherPriority.Background);

        Focus();
        Keyboard.Focus(this);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateSaveButtonPosition();

    private void UpdateSaveButtonPosition()
    {
        if (_saveButton == null) return;

        Canvas.SetLeft(
            _saveButton,
            (ActualWidth - _saveButton.Width) / 2
        );

        Canvas.SetTop(
            _saveButton,
            (ActualHeight - _saveButton.Height) / 2
        );
    }

    private void SaveAndClose()
    {
        _configManager.SaveGroup(_groupId, _regions);
        Close();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
        Keyboard.Focus(this);

        var pos = e.GetPosition(this);

        _selectedRegion = _regions.FindLast(r =>
        {
            var b = r.Value.Bounds;
            return new Rect(b.X, b.Y, b.Width, b.Height).Contains(pos);
        });

        if (_selectedRegion != null)
        {
            var b = _selectedRegion.Value.Bounds;
            _dragStart = pos;
            _originalRect = new Rect(b.X, b.Y, b.Width, b.Height);

            if (IsNearCorner(_originalRect.Value, pos))
                _isResizing = true;
            else
                _isDraggingRegion = true;

            CaptureMouse();
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        _startPoint = pos;
        _previewRect = null;
        _selectedRegion = null;
        _isDraggingRegion = false;
        _isResizing = false;

        CaptureMouse();
        InvalidateVisual();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(this);

        if (_isDraggingRegion && _selectedRegion != null && _dragStart != null && _originalRect != null)
        {
            var offset = pos - _dragStart.Value;
            var r = _originalRect.Value;

            _selectedRegion.Value.Bounds.X = r.X + offset.X;
            _selectedRegion.Value.Bounds.Y = r.Y + offset.Y;

            InvalidateVisual();
        }
        else if (_isResizing && _selectedRegion != null && _dragStart != null && _originalRect != null)
        {
            var offset = pos - _dragStart.Value;
            var r = _originalRect.Value;

            _selectedRegion.Value.Bounds.Width = Math.Max(10, r.Width + offset.X);
            _selectedRegion.Value.Bounds.Height = Math.Max(10, r.Height + offset.Y);

            InvalidateVisual();
        }
        else if (_startPoint != null && e.LeftButton == MouseButtonState.Pressed)
        {
            _previewRect = new Rect(_startPoint.Value, pos);
            InvalidateVisual();
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();

        if (_isDraggingRegion || _isResizing)
        {
            _isDraggingRegion = false;
            _isResizing = false;
            _dragStart = null;
            _originalRect = null;
            e.Handled = true;
            return;
        }

        if (_startPoint != null && _previewRect != null)
        {
            var rect = _previewRect.Value;

            if (rect.Width > 5 && rect.Height > 5)
            {
                const string regionName = "Globalna Kolejka Budowy / Global Build Queue";

                if (_regions.Any(r => r.Name == regionName))
                {
                    MessageBox.Show("Region \"Globalna Kolejka Budowy\" już istnieje. Usuń go (Delete), aby dodać nowy.");
                }
                else
                {
                    var color = PickColorDialog(this) ?? Color.FromArgb(120, 0, 255, 0);

                    _regions.Add(new NamedRegion
                    {
                        Name = regionName,
                        Value = new RegionEntry
                        {
                            Bounds = new RegionBounds
                            {
                                X = rect.X,
                                Y = rect.Y,
                                Width = rect.Width,
                                Height = rect.Height
                            },
                            Color = color.ToString()
                        }
                    });
                }
            }
        }

        _startPoint = null;
        _previewRect = null;
        _previewName = null;
        _previewColor = null;

        InvalidateVisual();
        e.Handled = true;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && _selectedRegion != null)
        {
            _regions.Remove(_selectedRegion);
            _selectedRegion = null;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        // S: zapis + zamknij (tak samo jak kliknięcie)
        if (e.Key == Key.S)
        {
            SaveAndClose();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        // Ctrl+PageUp: zapis + zamknij (ale nie ten pierwszy od otwarcia)
        if (e.Key == Key.PageUp && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (_ignoreFirstCtrlPageUp)
            {
                e.Handled = true;
                return;
            }

            SaveAndClose();
            e.Handled = true;
            return;
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        RegionOverlayRenderer.RenderRegions(dc, _regions, _previewRect, _previewName, _previewColor, _selectedRegion);
    }

    private static Color? PickColorDialog(Window? owner = null)
    {
        var dlg = new ColorPickerWindow { Owner = owner };
        return dlg.ShowDialog() == true ? dlg.SelectedColor : null;
    }

    private static bool IsNearCorner(Rect rect, Point pos)
    {
        return (Math.Abs(rect.Right - pos.X) < HANDLE_SIZE &&
                Math.Abs(rect.Bottom - pos.Y) < HANDLE_SIZE);
    }
}
