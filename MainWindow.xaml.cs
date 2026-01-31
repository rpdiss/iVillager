using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using iVillager.Capture;
using iVillager.Models;
using iVillager.Overlay;
using Color = System.Windows.Media.Color;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace iVillager;

public partial class MainWindow : Window
{
    private const string GroupId = "v1";
    private const string RegionName = "villager_in_que";
    private const int QueueGoneConfirmSeconds = 2;
    private const int ReminderFirstSeconds = 15;
    private const int ReminderSecondSeconds = 30;

    private bool _isRegionMonitoringRunning;
    private DispatcherTimer? _regionTimer;

    private readonly RegionConfigManager _configManager = new("region_config.json");
    private readonly ScreenCaptureService _screenCapture = new();
    private readonly IconDetectionService _iconDetection = new();
    private readonly MediaPlayer _soundPlayer = new();

    private AppSettings _appSettings;
    private NamedRegion? _cachedRegion;

    private bool _isWaitingForHotkeyRebind;

    private GlobalHotkeyHook? _globalHotkeyHook;
    private GlobalHotkeyHook? _regionOverlayHotkeyHook;

    private int _reminderElapsedSeconds;
    private int _reminderPhase;
    private bool _isVillagerQueued;
    private int _queueMissingSeconds;

    public MainWindow()
    {
        InitializeComponent();
        _appSettings = AppSettings.Load();

        PreviewKeyDown += Window_PreviewKeyDown;
        Loaded += OnLoaded;

        Closed += (_, _) =>
        {
            _globalHotkeyHook?.Dispose();
            _regionOverlayHotkeyHook?.Dispose();
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        HotkeyInput.Text = _appSettings.HotkeyStartStop;
        RegisterHotkey(_appSettings.HotkeyStartStop);

        OverlayHotkeyToggle.IsChecked = _appSettings.OverlayHotkeyEnabled;
        ApplyOverlayHotkeyState(_appSettings.OverlayHotkeyEnabled);

        _iconDetection.LoadTemplates(AppContext.BaseDirectory);
    }

    private void ApplyOverlayHotkeyState(bool enabled)
    {
        _regionOverlayHotkeyHook?.Dispose();
        _regionOverlayHotkeyHook = null;

        if (!enabled)
            return;

        _regionOverlayHotkeyHook = new GlobalHotkeyHook(
            Key.PageUp,
            ModifierKeys.Control,
            () => Dispatcher.Invoke(OpenRegionSelector),
            Dispatcher);
    }

    private void OverlayHotkeyToggle_Changed(object sender, RoutedEventArgs e)
    {
        var enabled = OverlayHotkeyToggle.IsChecked == true;
        _appSettings.OverlayHotkeyEnabled = enabled;
        _appSettings.Save();

        ApplyOverlayHotkeyState(enabled);
    }

    private void RegisterHotkey(string? shortcut)
    {
        _globalHotkeyHook?.Dispose();
        _globalHotkeyHook = null;

        if (!HotkeyHelper.TryParse(shortcut, out var key, out var modifiers))
            return;

        try
        {
            _globalHotkeyHook = new GlobalHotkeyHook(key, modifiers, () =>
            {
                Dispatcher.Invoke(() => StartStopButton_Click(null, null!));
            }, Dispatcher);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Nie mo¿na zarejestrowaæ skrótu: {ex.Message}",
                "Skrót klawiszowy",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_isWaitingForHotkeyRebind)
        {
            if (e.Key == Key.Escape)
            {
                _isWaitingForHotkeyRebind = false;
                RebindHotkeyButton.IsEnabled = true;
                HotkeyInput.Text = _appSettings.HotkeyStartStop ?? "";
                HotkeyInput.ToolTip = "np. Ctrl+Shift+F1";
                e.Handled = true;
                return;
            }

            if (IsModifierKey(e.Key))
                return;

            var modifiers = Keyboard.Modifiers;
            var key = e.Key;
            var shortcut = HotkeyHelper.ToString(key, modifiers);

            HotkeyInput.Text = shortcut;
            HotkeyInput.ToolTip = "np. Ctrl+Shift+F1";
            _isWaitingForHotkeyRebind = false;
            RebindHotkeyButton.IsEnabled = true;

            e.Handled = true;
            return;
        }

        if (_appSettings.OverlayHotkeyEnabled
            && e.Key == Key.PageUp
            && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            OpenRegionSelector();
            e.Handled = true;
        }
    }

    private static bool IsModifierKey(Key key) => key switch
    {
        Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin => true,
        _ => false
    };

    private void OpenRegionSelector()
    {
        var overlay = new RegionSelectorOverlay(GroupId);
        overlay.ShowDialog();
        _cachedRegion = null;
    }

    private void StartStopButton_Click(object? sender, RoutedEventArgs e)
    {
        _isRegionMonitoringRunning = !_isRegionMonitoringRunning;

        if (_isRegionMonitoringRunning)
        {
            StartStopButton.Content = "Stop";
            StartStopButton.Background = new SolidColorBrush(Color.FromRgb(0xB5, 0x2E, 0x2E));
            StartStopButton.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0x50, 0x50));
            StartRegionMonitoring();
        }
        else
        {
            StartStopButton.Content = "Start";
            StartStopButton.Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x73, 0x77));
            StartStopButton.BorderBrush = new SolidColorBrush(Color.FromRgb(0x14, 0xA3, 0xA8));
            StopRegionMonitoring();
        }
    }

    private void StartRegionMonitoring()
    {
        _iconDetection.ResetSessionLock();

        DetectedIconText.Visibility = Visibility.Collapsed;
        DetectedIconText.Text = "";

        _isVillagerQueued = false;
        _queueMissingSeconds = 0;

        ResetSoundReminder(silent: true);

        _regionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _regionTimer.Tick += OnRegionTick;
        _regionTimer.Start();

        OnRegionTick(null, EventArgs.Empty);
    }

    private void StopRegionMonitoring()
    {
        _regionTimer?.Stop();
        _regionTimer = null;
        _soundPlayer.Stop();
    }

    private void OnRegionTick(object? sender, EventArgs e)
    {
        var region = GetVillagerInQueRegion();
        if (region == null)
            return;

        try
        {
            using var bmp = _screenCapture.CaptureRegion(region);
            var detected = _iconDetection.DetectInRegion(bmp);

            if (detected != null)
            {
                _isVillagerQueued = true;
                _queueMissingSeconds = 0;

                DetectedIconText.Text = $"Wykryta ikona: {detected}";
                DetectedIconText.Visibility = Visibility.Visible;

                _soundPlayer.Stop();
                ResetSoundReminder(silent: true);
                return;
            }

            DetectedIconText.Visibility = Visibility.Collapsed;
            DetectedIconText.Text = "";

            if (_isVillagerQueued)
            {
                _queueMissingSeconds++;

                if (_queueMissingSeconds < QueueGoneConfirmSeconds)
                    return;

                _isVillagerQueued = false;
                _queueMissingSeconds = 0;

                ResetSoundReminder(silent: false);
                return;
            }

            _reminderElapsedSeconds++;

            if (_reminderPhase == -1 && _reminderElapsedSeconds >= ReminderFirstSeconds)
            {
                PlayReminderSound("ty_rob_wiesniaka.mp3");
                _reminderPhase = 0;
            }
            else if (_reminderPhase == 0 && _reminderElapsedSeconds >= ReminderSecondSeconds)
            {
                PlayReminderSound("uzyj_wieska.mp3");
                _reminderPhase = 1;
            }
            else if (_reminderPhase == 1
                     && _reminderElapsedSeconds > ReminderSecondSeconds
                     && (_reminderElapsedSeconds - ReminderSecondSeconds) % ReminderFirstSeconds == 0)
            {
                PlayReminderSound("uzyj_wieska.mp3");
            }
        }
        catch
        {
        }
    }

    private void PlayReminderSound(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", fileName);
        if (!File.Exists(path))
            return;

        _soundPlayer.Stop();
        _soundPlayer.Open(new Uri(path, UriKind.Absolute));
        _soundPlayer.Play();
    }

    private void ResetSoundReminder(bool silent = false)
    {
        _reminderElapsedSeconds = 0;
        _reminderPhase = -1;

        if (!silent)
            PlayReminderSound("rob_wiesniaka.mp3");
    }

    private NamedRegion? GetVillagerInQueRegion()
    {
        if (_cachedRegion != null)
            return _cachedRegion;

        var regions = _configManager.LoadGroup(GroupId);
        _cachedRegion = regions.Find(r => r.Name == RegionName);
        return _cachedRegion;
    }

    private void RebindHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        _isWaitingForHotkeyRebind = true;
        HotkeyInput.Text = "Naciœnij kombinacjê…";
        HotkeyInput.ToolTip = "Naciœnij dowoln¹ kombinacjê (Escape = anuluj)";
        RebindHotkeyButton.IsEnabled = false;
    }

    private void SaveHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isWaitingForHotkeyRebind)
            return;

        var shortcut = HotkeyInput.Text?.Trim();

        if (string.IsNullOrEmpty(shortcut)
            || shortcut == "Naciœnij kombinacjê…"
            || !HotkeyHelper.TryParse(shortcut, out _, out _))
        {
            MessageBox.Show(
                "Nieprawid³owy skrót. Kliknij Rebind i naciœnij kombinacjê lub wpisz np. Ctrl+Shift+F1.",
                "Skrót klawiszowy",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _appSettings.HotkeyStartStop = shortcut!;
        _appSettings.Save();

        RegisterHotkey(shortcut);

        MessageBox.Show(
            "Skrót zapisany. Dzia³a globalnie (tak¿e gdy gra ma fokus).",
            "Skrót klawiszowy",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
