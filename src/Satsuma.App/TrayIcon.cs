using System.Diagnostics;
using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;

namespace Satsuma.App;

/// <summary>The notification-area icon: the app's only permanent UI.</summary>
sealed class TrayIcon : IDisposable
{
    readonly Forms.NotifyIcon _icon;
    readonly Forms.ToolStripMenuItem _pause;

    public TrayIcon(Action openSettings, Action<bool> setPaused, Action quit)
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Satsuma.ico")).Stream;
        _pause = new Forms.ToolStripMenuItem("Pause") { CheckOnClick = true };
        _pause.CheckedChanged += (_, _) =>
        {
            setPaused(_pause.Checked);
            _icon!.Text = _pause.Checked ? "Satsuma (paused)" : "Satsuma";
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Settings", null, (_, _) => openSettings());
        menu.Items.Add(_pause);
        menu.Items.Add("Open log folder", null, (_, _) =>
        {
            Directory.CreateDirectory(AppSettings.Folder);
            Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { AppSettings.Folder } });
        });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit Satsuma", null, (_, _) => quit());

        _icon = new Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize),
            Text = "Satsuma",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
                openSettings();
        };
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Icon?.Dispose();
        _icon.Dispose();
    }
}
