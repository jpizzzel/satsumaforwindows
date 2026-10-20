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

    /// <summary>Core conversions plus the operations that need Windows: PDF rendering and the actions wheel.</summary>
    internal static OperationCatalog CreateCatalog(HudWindow hud) => OperationCatalog.CreateDefault()
        .Add(WheelMode.Convert, new PdfToImageOperation("PNG", "png"), new PdfToImageOperation("JPG", "jpg"))
        .Add(WheelMode.Actions,
            new CopyFilesAction(),
            new CutFilesAction(),
            new CopyPathAction(),
            new FloatAction(),
            new ShareAction(hud),
            new TerminalAction(),
            new RevealAction());

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        if (e.Args is ["--self-test", var outputFolder])
        {
            Shutdown(await SelfTest.RunAsync(outputFolder));
            return;
        }

        _singleInstance = new Mutex(true, @"Local\Satsuma.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        _settings = AppSettings.Load();
        var hud = new HudWindow();
        var jobs = new JobRunner(_settings, hud);
        _wheel = new WheelWindow(CreateCatalog(hud), _settings);
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
        // The drop reaches the wheel just after the button-up the hook sees, so give it a moment before
        // tidying up a wheel nothing was dropped on. A newer drag re-arming in the meantime is left alone.
        var armed = _armCount;
        await Task.Delay(400);
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
        base.OnExit(e);
    }

    static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // A tray utility should survive one bad file rather than vanish; the log keeps the details.
        Log.Write($"Unhandled: {e.Exception}");
        e.Handled = true;
    }
}
