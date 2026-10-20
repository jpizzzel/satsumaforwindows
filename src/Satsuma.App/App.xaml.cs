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

        if (e.Args is ["--self-test", var outputFolder, .. var rest])
        {
            Shutdown(await SelfTest.RunAsync(outputFolder, rest.FirstOrDefault()));
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
        // The hook sees the button go up a moment before the drop reaches the wheel, so wait a bit before
        // hiding it. If a new drag has already re-armed it, leave it alone.
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
        // Better to log one bad file and keep going than to vanish from the tray.
        Log.Write($"Unhandled: {e.Exception}");
        e.Handled = true;
    }
}
