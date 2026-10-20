using System.Runtime.InteropServices;
using System.Windows.Threading;
using Satsuma.Core;

namespace Satsuma.App.Interop;

/// <summary>
/// Watches the mouse system-wide with a low-level hook. Once the left button is held and the pointer has moved
/// past the drag threshold, it reports whenever the held modifier (and so the wheel the user wants) changes.
/// It never consumes input; the wheel window itself is the OLE drop target that receives the files.
/// </summary>
sealed class DragWatcher : IDisposable
{
    readonly Dispatcher _dispatcher;
    readonly Func<WheelMode?> _currentMode;
    readonly Native.LowLevelMouseProc _proc; // Held in a field so the GC can't collect the hook callback.
    IntPtr _hook;

    bool _buttonDown;
    bool _dragging;
    Native.POINT _downAt;
    WheelMode? _reported;

    /// <summary>The modifier changed during a drag. Null means no (enabled) modifier is held.</summary>
    public event Action<WheelMode?, Native.POINT>? ModeChanged;

    /// <summary>The left button was released, ending any drag.</summary>
    public event Action? Released;

    public bool Paused { get; set; }

    public DragWatcher(Dispatcher dispatcher, Func<WheelMode?> currentMode)
    {
        _dispatcher = dispatcher;
        _currentMode = currentMode;
        _proc = HookCallback;
        _hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _proc, Native.GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
            throw new InvalidOperationException($"Could not install the mouse hook (error {Marshal.GetLastPInvokeError()}).");
    }

    IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // Windows drops hooks that take longer than ~300 ms, so this only records state and posts events.
        if (nCode >= 0 && !Paused)
        {
            var info = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam);
            switch ((int)wParam)
            {
                case Native.WM_LBUTTONDOWN:
                    _buttonDown = true;
                    _dragging = false;
                    _reported = null;
                    _downAt = info.pt;
                    break;

                case Native.WM_MOUSEMOVE when _buttonDown:
                    if (!_dragging)
                    {
                        _dragging = Math.Abs(info.pt.X - _downAt.X) > Native.GetSystemMetrics(Native.SM_CXDRAG)
                            || Math.Abs(info.pt.Y - _downAt.Y) > Native.GetSystemMetrics(Native.SM_CYDRAG);
                    }
                    if (_dragging)
                    {
                        var mode = _currentMode();
                        if (mode != _reported)
                        {
                            _reported = mode;
                            var at = info.pt;
                            _dispatcher.BeginInvoke(() => ModeChanged?.Invoke(mode, at));
                        }
                    }
                    break;

                case Native.WM_LBUTTONUP when _buttonDown:
                    _buttonDown = false;
                    var wasDragging = _dragging;
                    _dragging = false;
                    _reported = null;
                    if (wasDragging)
                        _dispatcher.BeginInvoke(() => Released?.Invoke());
                    break;
            }
        }
        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            Native.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
