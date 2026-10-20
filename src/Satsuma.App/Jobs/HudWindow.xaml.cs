using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Satsuma.App.Interop;

namespace Satsuma.App.Jobs;

/// <summary>Stack of job cards in the bottom-right corner, like notifications that show progress.</summary>
public partial class HudWindow : Window
{
    readonly ObservableCollection<JobCard> _cards = [];
    Native.POINT _anchor;

    public HudWindow()
    {
        InitializeComponent();
        Cards.ItemsSource = _cards;
        Native.MakeNonActivating(new WindowInteropHelper(this).EnsureHandle());
        SizeChanged += (_, _) => Reposition();
    }

    public IntPtr Handle => new WindowInteropHelper(this).Handle;

    public JobCard Start(string title, string detail, string? glyph)
    {
        var card = new JobCard(this) { Title = title, Detail = detail, Glyph = glyph ?? "\uE895" };
        // Show up on whichever monitor you're working on, and stay there until the cards are gone.
        if (_cards.Count == 0)
            Native.GetCursorPos(out _anchor);
        _cards.Add(card);
        if (!IsVisible)
            Show();
        Reposition();
        return card;
    }

    internal void Remove(JobCard card)
    {
        _cards.Remove(card);
        if (_cards.Count == 0)
            Hide();
    }

    void Reposition()
    {
        var area = Native.WorkAreaAt(_anchor);
        Native.GetWindowRect(Handle, out var rect);
        Native.SetWindowPos(Handle, Native.HWND_TOPMOST, area.Right - (rect.Right - rect.Left) - 4,
            area.Bottom - (rect.Bottom - rect.Top) - 4, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    void Card_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is JobCard { Outputs: [var first, ..] } card)
        {
            Reveal(first);
            Remove(card);
        }
    }

    void Close_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is JobCard card)
            card.Dismiss();
        e.Handled = true;
    }

    /// <summary>Opens Explorer with the file selected, or opens the folder itself.</summary>
    public static void Reveal(string path)
    {
        if (Directory.Exists(path))
            Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { path } });
        else
            Process.Start("explorer.exe", $"/select,\"{path}\"");
    }
}

public sealed class JobCard(HudWindow hud) : INotifyPropertyChanged
{
    static readonly Brush Running = new SolidColorBrush(Color.FromRgb(0xF5, 0x84, 0x1F));
    static readonly Brush Succeeded = new SolidColorBrush(Color.FromRgb(0x4C, 0xC2, 0x6A));
    static readonly Brush Failed = new SolidColorBrush(Color.FromRgb(0xF0, 0x5A, 0x4F));

    readonly DispatcherTimer _autoClose = new();
    CancellationTokenSource? _cancel;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title { get; set => Set(ref field, value); } = "";
    public string Detail { get; set => Set(ref field, value); } = "";
    public string Glyph { get; set => Set(ref field, value); } = "";
    public Brush Accent { get; private set => Set(ref field, value); } = Running;
    public double Percent { get; private set => Set(ref field, value); }
    public bool Indeterminate { get; private set => Set(ref field, value); } = true;
    public bool IsRunning { get; private set => Set(ref field, value); } = true;
    public IReadOnlyList<string> Outputs { get; private set; } = [];
    public string? Hint { get; private set => Set(ref field, value); }
    public string CloseHint => IsRunning ? "Cancel" : "Dismiss";

    public void AttachCancellation(CancellationTokenSource cancel) => _cancel = cancel;

    public void Report(double fraction)
    {
        Indeterminate = false;
        Percent = Math.Clamp(fraction, 0, 1) * 100;
    }

    public void Succeed(string title, string detail, IReadOnlyList<string> outputs)
    {
        Finish(title, detail, "\uE73E", Succeeded, TimeSpan.FromSeconds(5));
        Outputs = outputs;
        Hint = outputs.Count > 0 ? "Click to show in File Explorer" : null;
    }

    public void Fail(string title, string detail) => Finish(title, detail, "\uE783", Failed, TimeSpan.FromSeconds(12));

    public void Dismiss()
    {
        if (IsRunning)
        {
            _cancel?.Cancel();
            return;
        }
        _autoClose.Stop();
        hud.Remove(this);
    }

    void Finish(string title, string detail, string glyph, Brush accent, TimeSpan closeAfter)
    {
        Title = title;
        Detail = detail;
        Glyph = glyph;
        Accent = accent;
        IsRunning = false;
        OnPropertyChanged(nameof(CloseHint));
        _autoClose.Interval = closeAfter;
        _autoClose.Tick += (_, _) => Dismiss();
        _autoClose.Start();
    }

    void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        OnPropertyChanged(name);
    }

    void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
