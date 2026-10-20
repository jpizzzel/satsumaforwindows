using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Satsuma.App.Interop;

namespace Satsuma.App.Operations;

/// <summary>A small always-on-top shelf holding files until they are dragged somewhere else.</summary>
public partial class FloatWindow : Window
{
    public sealed record Item(string Path, string Name, ImageSource? Icon);

    readonly ObservableCollection<Item> _items = [];
    Point? _pressedAt;

    public FloatWindow(IEnumerable<string> paths)
    {
        InitializeComponent();
        Items.ItemsSource = _items;
        Add(paths);
        Native.MakeNonActivating(new WindowInteropHelper(this).EnsureHandle());
        Drop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
                Add(files);
        };
    }

    public void ShowNearCursor()
    {
        Native.GetCursorPos(out var cursor);
        var scale = Native.ScaleAt(cursor);
        Left = cursor.X / scale + 12;
        Top = cursor.Y / scale - 20;
        Show();
    }

    void Add(IEnumerable<string> paths)
    {
        foreach (var path in paths)
            if (_items.All(i => !string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase)))
                _items.Add(new Item(path, System.IO.Path.GetFileName(path.TrimEnd('\\')), ShellIcon(path)));
        Caption.Text = _items.Count == 1 ? "1 item" : $"{_items.Count} items";
    }

    void Header_MouseDown(object sender, MouseButtonEventArgs e) => DragMove();

    void Close_Click(object sender, RoutedEventArgs e) => Close();

    void Items_MouseDown(object sender, MouseButtonEventArgs e) => _pressedAt = e.GetPosition(this);

    void Items_MouseMove(object sender, MouseEventArgs e)
    {
        if (_pressedAt is not { } start || e.LeftButton != MouseButtonState.Pressed)
            return;
        var delta = e.GetPosition(this) - start;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _pressedAt = null;
        var data = new DataObject(DataFormats.FileDrop, _items.Select(i => i.Path).ToArray());
        DragDrop.DoDragDrop(Items, data, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);

        // Whatever was moved away is gone from the shelf; close once it is empty.
        foreach (var item in _items.Where(i => !File.Exists(i.Path) && !Directory.Exists(i.Path)).ToList())
            _items.Remove(item);
        if (_items.Count == 0)
            Close();
        else
            Add([]);
    }

    static ImageSource? ShellIcon(string path)
    {
        var info = new SHFILEINFO();
        if (SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON) == IntPtr.Zero
            || info.hIcon == IntPtr.Zero)
            return null;
        try
        {
            var image = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    const uint SHGFI_ICON = 0x100;
    const uint SHGFI_LARGEICON = 0x0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr hIcon);
}
