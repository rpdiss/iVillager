using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Threading;

namespace iVillager;

/// <summary>
/// Low-level keyboard hook – działa też gdy gra ma fullscreen (w przeciwieństwie do RegisterHotKey).
/// </summary>
public sealed class GlobalHotkeyHook : IDisposable
{
    private readonly IntPtr _hookId;
    private readonly LowLevelKeyboardProc _proc;
    private readonly GCHandle _procHandle;
    private readonly Key _key;
    private readonly ModifierKeys _modifiers;
    private readonly Action _onTrigger;
    private readonly Dispatcher _dispatcher;
    private bool _disposed;
    private long _lastTriggerTicks;
    private const int DebounceMs = 400;

    public GlobalHotkeyHook(Key key, ModifierKeys modifiers, Action onTrigger, Dispatcher? dispatcher = null)
    {
        _key = key;
        _modifiers = modifiers;
        _onTrigger = onTrigger;
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
        _proc = HookCallback;
        _procHandle = GCHandle.Alloc(_proc);
        _hookId = SetHook(_proc);
    }

    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        var mod = curModule != null ? GetModuleHandle(curModule.ModuleName) : IntPtr.Zero;
        return SetWindowsHookEx(WH_KEYBOARD_LL, proc, mod, 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            var kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var key = KeyInterop.KeyFromVirtualKey((int)kbd.vkCode);
            var mods = GetCurrentModifiers();
            if (key == _key && mods == _modifiers)
            {
                var now = Environment.TickCount64;
                if (now - _lastTriggerTicks >= DebounceMs)
                {
                    _lastTriggerTicks = now;
                    _dispatcher.Invoke(_onTrigger);
                }
                return (IntPtr)1;
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static ModifierKeys GetCurrentModifiers()
    {
        var m = ModifierKeys.None;
        if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0) m |= ModifierKeys.Control;
        if ((GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0) m |= ModifierKeys.Shift;
        if ((GetAsyncKeyState(VK_MENU) & 0x8000) != 0) m |= ModifierKeys.Alt;
        if ((GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0) m |= ModifierKeys.Windows;
        return m;
    }

    public void Dispose()
    {
        if (_disposed) return;
        UnhookWindowsHookEx(_hookId);
        _procHandle.Free();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int VK_CONTROL = 0x11;
    private const int VK_SHIFT = 0x10;
    private const int VK_MENU = 0x12;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
