using System.IO;
using System.Windows;
using System.Windows.Interop;
using Satsuma.App.Interop;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Satsuma.App.Operations;

static class ShareSheet
{
    // The share sheet needs a real window to belong to. The progress cards come and go, so it gets its own:
    // a 1x1 see-through window parked under the cursor, kept around and reused.
    static Window? _owner;

    /// <summary>Shows the system share UI for files.</summary>
    public static async Task ShowAsync(IReadOnlyList<string> paths)
    {
        var items = new List<IStorageItem>();
        foreach (var path in paths)
        {
            items.Add(Directory.Exists(path)
                ? await StorageFolder.GetFolderFromPathAsync(path)
                : await StorageFile.GetFileFromPathAsync(path));
        }

        var window = Owner();
        var manager = DataTransferManagerInterop.GetForWindow(window);
        void OnRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            sender.DataRequested -= OnRequested;
            args.Request.Data.Properties.Title = items.Count == 1 ? items[0].Name : $"{items.Count} items";
            args.Request.Data.SetStorageItems(items);
        }
        manager.DataRequested += OnRequested;
        try
        {
            DataTransferManagerInterop.ShowShareUIForWindow(window);
        }
        catch
        {
            // Don't leave the handler behind to answer the next share with these files.
            manager.DataRequested -= OnRequested;
            throw;
        }
    }

    static IntPtr Owner()
    {
        _owner ??= new Window
        {
            Title = "Satsuma share",
            Width = 1,
            Height = 1,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize,
            Topmost = true,
        };

        Native.GetCursorPos(out var cursor);
        _owner.Show();
        var hwnd = new WindowInteropHelper(_owner).Handle;
        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, cursor.X, cursor.Y, 0, 0, Native.SWP_NOSIZE);
        _owner.Activate();
        return hwnd;
    }
}
