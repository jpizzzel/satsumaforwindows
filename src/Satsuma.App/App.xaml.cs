using System.Windows;
using System.Windows.Threading;
using Satsuma.App.Interop;
using Satsuma.App.Jobs;
using Satsuma.App.Operations;
using Satsuma.App.Wheel;
using Satsuma.Core;

namespace Satsuma.App;

public partial class App : Application
{
    Mutex? _singleInstance;
    AppSettings _settings = new();
    DragWatcher? _watcher;
    WheelWindow? _wheel;
    TrayIcon? _tray;
    SettingsWindow? _settingsWindow;
    int _armCount;

    const string OpenSettingsSignal = @"Local\Satsuma.OpenSettings";

    /// <summary>Core conversions plus the operations that need Windows: PDF rendering and the actions wheel.</summary>
    internal static OperationCatalog CreateCatalog() => OperationCatalog.CreateDefault()
        .Add(WheelMode.Convert, new PdfToImageOperation("PNG", "png"), new PdfToImageOperation("JPG", "jpg"))
        .Add(WheelMode.Actions,
            new CopyFilesAction(),
            new CutFilesAction(),
            new CopyPathAction(),
            new FloatAction(),
            new ShareAction(),
            new TerminalAction(),
            new RevealAction());

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log.Write($"Crashed: {args.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Write($"Unobserved: {args.Exception}");
            args.SetObserved();
        };

        if (e.Args is ["--self-test", var outputFolder, .. var rest])
        {
            try
            {
                Shutdown(await SelfTest.RunAsync(outputFolder, rest.FirstOrDefault()));
            }
            catch (Exception ex)
            {
                Log.Write($"Self-test blew up: {ex}");
                Shutdown(2);
            }
            return;
        }

        _singleInstance = new Mutex(true, @"Local\Satsuma.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            // Already running in the tray. Starting it again is how people look for the settings, so open them.
            if (EventWaitHandle.TryOpenExisting(OpenSettingsSignal, out var signal))
                signal.Set();
            Shutdown();
            return;
        }
        var openSettings = new EventWaitHandle(false, EventResetMode.AutoReset, OpenSettingsSignal);
        ThreadPool.RegisterWaitForSingleObject(openSettings,
            (_, _) => Dispatcher.BeginInvoke(OpenSettings), null, Timeout.Infinite, executeOnlyOnce: false);

        _settings = AppSettings.Load();
        var hud = new HudWindow();
        var jobs = new JobRunner(_settings, hud);
        _wheel = new WheelWindow(CreateCatalog(), _settings);
        _wheel.Chosen += jobs.Run;

        _watcher = new DragWatcher(Dispatcher, () => _settings.ModeFor(
            shift: Native.IsKeyDown(Native.VK_SHIFT),
            control: Native.IsKeyDown(Native.VK_CONTROL),
            alt: Native.IsKeyDown(Native.VK_MENU)));
        _watcher.ModeChanged += OnModeChanged;
        _watcher.Released += OnReleased;

        _tray = new TrayIcon(OpenSettings, paused => _watcher.Paused = paused, () => Shutdown());

        if (!_settings.Welcomed)
        {
            _settings.Welcomed = true;
            _settings.Save();
            OpenSettings();
        }
    }

    void OnModeChanged(WheelMode? mode, Native.POINT at)
    {
        if (_wheel is null)
            return;
        if (mode is null)
        {
            if (_wheel.IsArmed)
                _wheel.Disarm();
        }
        else if (!_wheel.IsArmed)
        {
            _armCount++;
            _wheel.Arm(mode.Value, at);
        }
        else
        {
            _wheel.SetMode(mode.Value);
        }
    }

    async void OnReleased()
    {
        if (_wheel is not { IsArmed: true })
            return;

        // Nothing with files ever reached the wheel (selecting text, a Ctrl+drag rubber band...), so get
        // the invisible catcher out of the way right now before it eats the next click.
        if (!_wheel.HasDragOver)
        {
            _wheel.Disarm();
            return;
        }

        // Files were over the wheel when the button went up, and the drop lands a moment after the hook
        // sees that. Give it time, then tidy up if it never came. A newer drag re-arming it is left alone.
        var armed = _armCount;
        await Task.Delay(1500);
        if (_wheel is { IsArmed: true } && armed == _armCount)
            _wheel.Disarm();
    }

    void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow(_settings);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _watcher?.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();

        // Let go of any leftover WinRT objects (the PDF renderer especially) while Windows' graphics stack is
        // still around. Left for process teardown, they crash the app on the way out (0xC000000D).
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        base.OnExit(e);
    }

    static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Better to log one bad file and keep going than to vanish from the tray.
        Log.Write($"Unhandled: {e.Exception}");
        e.Handled = true;
    }
}
