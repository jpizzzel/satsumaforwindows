using System.Runtime.InteropServices;
using System.Windows.Threading;
using Satsuma.Core;

namespace Satsuma.App.Interop;

/// <summary>
/// Watches the mouse system-wide with a low-level hook. Once the left button is down and the mouse has moved
/// past the drag threshold, it tells us whenever the held modifier changes, which decides the wheel.
/// It never swallows any input. The wheel window is the real drop target.
/// </summary>
sealed class DragWatcher : IDisposable
{
    readonly Dispatcher _dispatcher;
    readonly Func<WheelMode?> _currentMode;
    readonly Native.LowLevelMouseProc _proc; // Held in a field so the GC can't collect the hook callback.
    readonly Thread _thread;
    IntPtr _hook;
    uint _threadId;

    bool _buttonDown;
    bool _dragging;
    Native.POINT _downAt;
    WheelMode? _reported;
    volatile bool _paused;

    /// <summary>The modifier changed during a drag. Null means no (enabled) modifier is held.</summary>
    public event Action<WheelMode?, Native.POINT>? ModeChanged;

    /// <summary>The left button was released, ending any drag.</summary>
    public event Action? Released;

    public bool Paused
    {
        get => _paused;
        set
        {
            _paused = value;
            // Forget any half-seen drag so we don't wake up thinking the button is still down.
            _dispatcher.BeginInvoke(() => Released?.Invoke());
        }
    }

    public DragWatcher(Dispatcher dispatcher, Func<WheelMode?> currentMode)
    {
        _dispatcher = dispatcher;
        _currentMode = currentMode;
        _proc = HookCallback;

        // The hook gets its own thread and message loop. If it lived on the UI thread, any hiccup there
        // longer than ~300 ms would make Windows quietly unhook us, and the whole app would just stop working.
        var installed = new TaskCompletionSource<int>();
        _thread = new Thread(() =>
        {
            _threadId = Native.GetCurrentThreadId();
            _hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _proc, Native.GetModuleHandle(null), 0);
            installed.SetResult(_hook == IntPtr.Zero ? Marshal.GetLastPInvokeError() : 0);
            if (_hook == IntPtr.Zero)
                return;
            while (Native.GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
                Native.DispatchMessage(ref message);
            Native.UnhookWindowsHookEx(_hook);
        })
        { IsBackground = true, Name = "Satsuma mouse hook" };
        _thread.Start();

        var error = installed.Task.GetAwaiter().GetResult();
        if (error != 0)
            throw new InvalidOperationException($"Could not install the mouse hook (error {error}).");
    }

    IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // Windows kills hooks that take more than ~300 ms, so just note what happened and post it.
        if (nCode >= 0 && !_paused)
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

                case Native.WM_MOUSEMOVE when _buttonDown && !Native.IsKeyDown(Native.VK_LBUTTON):
                    // We missed the button-up (a UAC prompt, Ctrl+Alt+Del, an admin window...). Treat it as one.
                    EndDrag();
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
                    EndDrag();
                    break;
            }
        }
        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    void EndDrag()
    {
        _buttonDown = false;
        var wasDragging = _dragging;
        _dragging = false;
        _reported = null;
        if (wasDragging)
            _dispatcher.BeginInvoke(() => Released?.Invoke());
    }

    public void Dispose()
    {
        if (_threadId != 0 && _thread.IsAlive)
        {
            Native.PostThreadMessage(_threadId, Native.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            _thread.Join(TimeSpan.FromSeconds(1));
        }
    }
}
