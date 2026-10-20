using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using Satsuma.Core;

namespace Satsuma.App;

sealed class AppSettings
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunValue = "Satsuma";

    public static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Satsuma");

    static string FilePath => Path.Combine(Folder, "settings.json");

    public bool ConvertEnabled { get; set; } = true;
    public bool ToolsEnabled { get; set; } = true;
    public bool ActionsEnabled { get; set; } = true;

    /// <summary>Null saves results next to the original file.</summary>
    public string? OutputFolder { get; set; }

    public int ImageQuality { get; set; } = 90;

    /// <summary>False until the welcome screen has been shown once.</summary>
    public bool Welcomed { get; set; }

    /// <summary>
    /// Shift = convert, Ctrl = tools, Alt = actions (roughly Shift, Cmd and Control on a Mac).
    /// If more than one is down, the more specific wheel wins.
    /// </summary>
    public WheelMode? ModeFor(bool shift, bool control, bool alt) =>
        alt && ActionsEnabled ? WheelMode.Actions
        : control && ToolsEnabled ? WheelMode.Tools
        : shift && ConvertEnabled ? WheelMode.Convert
        : null;

    public OutputLocator Output() => new() { Folder = string.IsNullOrWhiteSpace(OutputFolder) ? null : OutputFolder };

    public ConversionOptions Options() => new() { ImageQuality = Math.Clamp(ImageQuality, 1, 100) };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            Log.Write($"Settings could not be read, using defaults: {e.Message}");
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            // Write to a temp file and swap it in, so a crash halfway can't leave you with broken settings.
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Write($"Settings could not be saved: {e.Message}");
        }
    }

    /// <summary>Lives in the registry instead of settings.json, so it always matches what Windows will actually do.</summary>
    public static bool LaunchAtStartup
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(RunValue) is string;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value)
                key.SetValue(RunValue, $"\"{Environment.ProcessPath}\"");
            else
                key.DeleteValue(RunValue, throwOnMissingValue: false);
        }
    }
}

static class Log
{
    static readonly Lock Gate = new();

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppSettings.Folder);
                File.AppendAllText(Path.Combine(AppSettings.Folder, "log.txt"), $"{DateTime.Now:s} {message}{Environment.NewLine}");
            }
        }
        catch (IOException)
        {
            // Logging must never take the app down.
        }
    }
}
