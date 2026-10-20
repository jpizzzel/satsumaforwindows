using System.Windows;
using Microsoft.Win32;
using Satsuma.Core.Media;

namespace Satsuma.App;

public partial class SettingsWindow : Window
{
    readonly AppSettings _settings;
    string? _folder;

    internal SettingsWindow(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();

        ConvertEnabled.IsChecked = settings.ConvertEnabled;
        ToolsEnabled.IsChecked = settings.ToolsEnabled;
        ActionsEnabled.IsChecked = settings.ActionsEnabled;
        _folder = settings.OutputFolder;
        SaveBeside.IsChecked = _folder is null;
        SaveInFolder.IsChecked = _folder is not null;
        SaveInFolder.IsEnabled = _folder is not null;
        FolderLabel.Text = _folder ?? "Save in a folder of your choice";
        Quality.Value = settings.ImageQuality;
        LaunchAtStartup.IsChecked = AppSettings.LaunchAtStartup;
        FfmpegStatus.Text = Ffmpeg.Locate() is { } ffmpeg
            ? $"Audio and video use ffmpeg from {ffmpeg}."
            : "ffmpeg was not found, so audio and video conversions are unavailable. Put ffmpeg.exe in the "
              + "'tools' folder next to Satsuma.exe, or install it so it is on PATH.";
    }

    void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Save converted files in", InitialDirectory = _folder };
        if (dialog.ShowDialog(this) != true)
            return;
        _folder = dialog.FolderName;
        FolderLabel.Text = _folder;
        SaveInFolder.IsEnabled = true;
        SaveInFolder.IsChecked = true;
    }

    void Done_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _settings.ConvertEnabled = ConvertEnabled.IsChecked == true;
        _settings.ToolsEnabled = ToolsEnabled.IsChecked == true;
        _settings.ActionsEnabled = ActionsEnabled.IsChecked == true;
        _settings.OutputFolder = SaveInFolder.IsChecked == true ? _folder : null;
        _settings.ImageQuality = (int)Quality.Value;
        _settings.Save();
        try
        {
            if (AppSettings.LaunchAtStartup != (LaunchAtStartup.IsChecked == true))
                AppSettings.LaunchAtStartup = LaunchAtStartup.IsChecked == true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log.Write($"Could not change the startup setting: {ex.Message}");
        }
        base.OnClosed(e);
    }
}
