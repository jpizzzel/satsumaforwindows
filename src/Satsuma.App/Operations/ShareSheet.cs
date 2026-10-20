using System.IO;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Satsuma.App.Operations;

static class ShareSheet
{
    /// <summary>Shows the system share UI for files, anchored to a desktop window.</summary>
    public static async Task ShowAsync(IReadOnlyList<string> paths, IntPtr window)
    {
        var items = new List<IStorageItem>();
        foreach (var path in paths)
        {
            items.Add(Directory.Exists(path)
                ? await StorageFolder.GetFolderFromPathAsync(path)
                : await StorageFile.GetFileFromPathAsync(path));
        }

        var manager = DataTransferManagerInterop.GetForWindow(window);
        void OnRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            sender.DataRequested -= OnRequested;
            args.Request.Data.Properties.Title = items.Count == 1 ? items[0].Name : $"{items.Count} items";
            args.Request.Data.SetStorageItems(items);
        }
        manager.DataRequested += OnRequested;
        DataTransferManagerInterop.ShowShareUIForWindow(window);
    }
}
