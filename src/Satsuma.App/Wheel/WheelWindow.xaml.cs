using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Satsuma.App.Interop;
using Satsuma.Core;
using Path = System.Windows.Shapes.Path;

namespace Satsuma.App.Wheel;

/// <summary>
/// The wheel that pops up under the cursor mid-drag. It starts out as an invisible drop target. Once a drag
/// with files moves over it, it draws the options for the current wheel and runs whatever you drop on.
/// Dropping on the hub cancels.
/// </summary>
public partial class WheelWindow : Window
{
    static readonly Brush Catcher = Frozen(Color.FromArgb(1, 0, 0, 0)); // Alpha 1 keeps it hit-testable but invisible.
    static readonly Brush SegmentFill = Frozen(Color.FromArgb(0xF0, 0x24, 0x22, 0x21));
    static readonly Brush SegmentStroke = Frozen(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
    static readonly Brush HubFill = Frozen(Color.FromArgb(0xF8, 0x18, 0x17, 0x16));
    static readonly Brush Text = Frozen(Colors.White);
    static readonly Brush DimText = Frozen(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF));
    static readonly FontFamily TextFont = new("Segoe UI Variable Text, Segoe UI");
    static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    static readonly Dictionary<WheelMode, (string Title, Brush Accent)> Modes = new()
    {
        [WheelMode.Convert] = ("Convert", Frozen(Color.FromRgb(0xF5, 0x84, 0x1F))),
        [WheelMode.Tools] = ("Tools", Frozen(Color.FromRgb(0x1F, 0xA3, 0x8F))),
        [WheelMode.Actions] = ("Actions", Frozen(Color.FromRgb(0x7A, 0x6C, 0xF0))),
    };

    readonly OperationCatalog _catalog;
    readonly AppSettings _settings;
    readonly List<Path> _segments = [];
    IReadOnlyList<string>? _files;
    IReadOnlyList<FileOperation> _operations = [];
    WheelLayout _layout = WheelLayout.For(0);
    WheelMode _mode;
    int? _hovered;

    static double Center => WheelLayout.WindowSize / 2;

    public bool IsArmed { get; private set; }

    /// <summary>True while a drag with files is actually over the wheel, so a drop may still be on its way.</summary>
    public bool HasDragOver { get; private set; }

    /// <summary>Files were dropped on an operation.</summary>
    public event Action<FileOperation, IReadOnlyList<string>>? Chosen;

    internal WheelWindow(OperationCatalog catalog, AppSettings settings)
    {
        _catalog = catalog;
        _settings = settings;
        InitializeComponent();
        Width = Height = Surface.Width = Surface.Height = WheelLayout.WindowSize;

        Native.MakeNonActivating(new WindowInteropHelper(this).EnsureHandle());
        DragEnter += OnDragEnter;
        DragOver += (_, e) => Track(e);
        DragLeave += (_, _) =>
        {
            HasDragOver = false;
            Hover(null);
        };
        Drop += OnDrop;
    }

    static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    /// <summary>Puts an invisible drop target under the cursor, ready to show the wheel when files arrive.</summary>
    internal void Arm(WheelMode mode, Native.POINT at)
    {
        _mode = mode;
        _files = null;
        _operations = [];
        _hovered = null;
        IsArmed = true;
        Surface.Children.Clear();
        _segments.Clear();
        Surface.Children.Add(Disc(Center, Catcher));
        PlaceAt(at);
        if (!IsVisible)
        {
            Show();
            PlaceAt(at); // The first Show() can apply WPF's own idea of where the window goes.
        }
    }

    public void Disarm()
    {
        IsArmed = false;
        HasDragOver = false;
        _files = null;
        Hide();
        Surface.Children.Clear();
        _segments.Clear();
    }

    public void SetMode(WheelMode mode)
    {
        if (mode == _mode)
            return;
        _mode = mode;
        if (_files is not null)
            Populate();
    }

    void PlaceAt(Native.POINT at)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var expected = (int)Math.Round(WheelLayout.WindowSize * Native.ScaleAt(at));
        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, at.X - expected / 2, at.Y - expected / 2, 0, 0,
            Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);

        // Moving to a monitor with a different DPI makes WPF resize the window, so re-center on whatever size it picked.
        Native.GetWindowRect(hwnd, out var rect);
        var (width, height) = (rect.Right - rect.Left, rect.Bottom - rect.Top);
        if (width != expected || height != expected)
            Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, at.X - width / 2, at.Y - height / 2, 0, 0,
                Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    void OnDragEnter(object sender, DragEventArgs e)
    {
        if (IsArmed && _files is null)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
            {
                _files = files;
                Populate();
            }
            else
            {
                // Not files (text, a link, etc), so get out of the way and let it drop wherever it was headed.
                Disarm();
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }
        }
        HasDragOver = IsArmed && _files is not null;
        Track(e);
    }

    WheelSlot? Track(DragEventArgs e)
    {
        e.Handled = true;
        e.Effects = DragDropEffects.None;
        var mode = _settings.ModeFor(
            shift: e.KeyStates.HasFlag(DragDropKeyStates.ShiftKey),
            control: e.KeyStates.HasFlag(DragDropKeyStates.ControlKey),
            alt: e.KeyStates.HasFlag(DragDropKeyStates.AltKey));
        if (!IsArmed || mode is null)
        {
            // Letting go of the key puts the drag back to normal.
            Disarm();
            return null;
        }

        SetMode(mode.Value);
        var point = e.GetPosition(Surface);
        var hit = _layout.HitTest(point.X - Center, point.Y - Center);
        Hover(hit?.Index);
        if (hit is not null)
            e.Effects = DragDropEffects.Copy; // Copy, never Move, so Explorer leaves the original alone.
        return hit;
    }

    void OnDrop(object sender, DragEventArgs e)
    {
        var hit = Track(e);
        var files = _files;
        var operations = _operations;
        Disarm();
        // Run it after Drop returns. Explorer sits waiting inside the drop until we're done with it.
        if (hit is { } slot && files is not null && slot.Index < operations.Count)
            Dispatcher.BeginInvoke(() => Chosen?.Invoke(operations[slot.Index], files));
    }

    void Populate()
    {
        _operations = _catalog.For(_mode, _files ?? []);
        _layout = WheelLayout.For(_operations.Count);
        _hovered = null;
        Draw();
    }

    void Draw()
    {
        Surface.Children.Clear();
        _segments.Clear();
        var accent = Modes[_mode].Accent;

        // Covers the whole disc so flicking a little past the edge still hits the outer ring.
        Surface.Children.Add(Disc(Center, Catcher));

        foreach (var slot in _layout.Slots)
        {
            var segment = new Path
            {
                Data = Sector(slot),
                Fill = SegmentFill,
                Stroke = SegmentStroke,
                StrokeThickness = 1,
            };
            _segments.Add(segment);
            Surface.Children.Add(segment);
            AddLabel(_operations[slot.Index], slot);
        }

        Surface.Children.Add(Disc(WheelLayout.HubRadius - 3, HubFill, accent));
        var summary = _files is null ? "" : _files.Count == 1 ? System.IO.Path.GetFileName(_files[0].TrimEnd('\\')) : $"{_files.Count} items";
        var hub = new StackPanel { Width = 2 * (WheelLayout.HubRadius - 8), IsHitTestVisible = false };
        hub.Children.Add(new TextBlock
        {
            Text = Modes[_mode].Title,
            Foreground = accent,
            FontFamily = TextFont,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            TextAlignment = TextAlignment.Center,
        });
        hub.Children.Add(new TextBlock
        {
            Text = _operations.Count == 0 ? "Nothing for these files" : summary,
            Foreground = DimText,
            FontFamily = TextFont,
            FontSize = 10,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = _operations.Count == 0 ? TextWrapping.Wrap : TextWrapping.NoWrap,
        });
        Place(hub, Center, Center);
    }

    void Hover(int? index)
    {
        if (index == _hovered)
            return;
        if (_hovered is { } old && old < _segments.Count)
            _segments[old].Fill = SegmentFill;
        if (index is { } current && current < _segments.Count)
            _segments[current].Fill = Modes[_mode].Accent;
        _hovered = index;
    }

    void AddLabel(FileOperation operation, WheelSlot slot)
    {
        var label = new StackPanel { IsHitTestVisible = false, MaxWidth = WheelLayout.RingWidth + 10 };
        if (operation.Glyph is { } glyph)
        {
            label.Children.Add(new TextBlock
            {
                Text = glyph,
                FontFamily = IconFont,
                FontSize = 16,
                Foreground = Text,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2),
            });
        }
        label.Children.Add(new TextBlock
        {
            Text = operation.Label,
            FontFamily = TextFont,
            FontSize = operation.Glyph is null ? 13 : 11,
            FontWeight = operation.Glyph is null ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = Text,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        });
        var (x, y) = WheelSlot.PointAt(slot.MidRadius, slot.MidAngle);
        Place(label, Center + x, Center + y);
    }

    void Place(FrameworkElement element, double centerX, double centerY)
    {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(element, centerX - element.DesiredSize.Width / 2);
        Canvas.SetTop(element, centerY - element.DesiredSize.Height / 2);
        Surface.Children.Add(element);
    }

    static Ellipse Disc(double radius, Brush fill, Brush? stroke = null)
    {
        var disc = new Ellipse
        {
            Width = 2 * radius,
            Height = 2 * radius,
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = stroke is null ? 0 : 2,
        };
        Canvas.SetLeft(disc, Center - radius);
        Canvas.SetTop(disc, Center - radius);
        return disc;
    }

    static Geometry Sector(WheelSlot slot)
    {
        var center = new Point(Center, Center);
        if (slot.Sweep >= 359.99)
        {
            return new CombinedGeometry(GeometryCombineMode.Exclude,
                new EllipseGeometry(center, slot.OuterRadius, slot.OuterRadius),
                new EllipseGeometry(center, slot.InnerRadius, slot.InnerRadius));
        }

        Point At(double radius, double angle)
        {
            var (x, y) = WheelSlot.PointAt(radius, angle);
            return new Point(Center + x, Center + y);
        }

        var end = slot.StartAngle + slot.Sweep;
        var large = slot.Sweep > 180;
        var figure = new PathFigure { StartPoint = At(slot.OuterRadius, slot.StartAngle), IsClosed = true };
        figure.Segments.Add(new ArcSegment(At(slot.OuterRadius, end), new Size(slot.OuterRadius, slot.OuterRadius), 0, large,
            SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(At(slot.InnerRadius, end), true));
        figure.Segments.Add(new ArcSegment(At(slot.InnerRadius, slot.StartAngle), new Size(slot.InnerRadius, slot.InnerRadius), 0,
            large, SweepDirection.Counterclockwise, true));
        var geometry = new PathGeometry([figure]);
        geometry.Freeze();
        return geometry;
    }

    /// <summary>Draws the wheel for some files to a PNG without showing it. Used by the --self-test check.</summary>
    internal IReadOnlyList<FileOperation> RenderPreview(IReadOnlyList<string> files, WheelMode mode, int hovered, string pngPath)
    {
        _files = files;
        _mode = mode;
        Populate();
        Hover(hovered);

        Surface.Measure(new Size(WheelLayout.WindowSize, WheelLayout.WindowSize));
        Surface.Arrange(new Rect(0, 0, WheelLayout.WindowSize, WheelLayout.WindowSize));
        Surface.UpdateLayout();
        var pixels = (int)WheelLayout.WindowSize * 2;
        var bitmap = new RenderTargetBitmap(pixels, pixels, 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(Surface);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(pngPath))
            encoder.Save(stream);

        var operations = _operations;
        Disarm();
        return operations;
    }
}
