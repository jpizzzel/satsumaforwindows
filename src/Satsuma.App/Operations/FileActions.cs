using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Satsuma.App.Jobs;
using Satsuma.Core;

namespace Satsuma.App.Operations;

/// <summary>Actions run on the UI thread and apply to any files or folders.</summary>
abstract class FileAction(string id, string label, string glyph, string description) : FileOperation
{
    public override string Id => "action." + id;
    public override string Label => label;
    public override string Glyph => glyph;
    public override string Description => description;
    public override bool RunsOnUiThread => true;

    public override bool AppliesTo(IReadOnlyList<string> inputs) => inputs.Count > 0;

    public override Task<IReadOnlyList<string>> RunAsync(JobContext context)
    {
        Run(context.Inputs);
        return Task.FromResult<IReadOnlyList<string>>([]);
    }

    protected virtual void Run(IReadOnlyList<string> inputs) => throw new NotSupportedException();

    protected static string FolderOf(string path) =>
        Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? path;
}

/// <summary>Copies the files themselves, ready to paste in Explorer, mail or chat apps.</summary>
sealed class CopyFilesAction() : FileAction("copy", "Copy", "", "Copying")
{
    protected override void Run(IReadOnlyList<string> inputs) => SetFiles(inputs, move: false);

    internal static void SetFiles(IReadOnlyList<string> inputs, bool move)
    {
        var files = new StringCollection();
        files.AddRange([.. inputs]);
        var data = new DataObject();
        data.SetFileDropList(files);
        // Explorer reads this to decide whether pasting copies or moves (DROPEFFECT_COPY = 1, DROPEFFECT_MOVE = 2).
        data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(move ? 2 : 1)));
        Clipboard.SetDataObject(data, copy: true);
    }
}

/// <summary>Cuts the files: pasting them in Explorer moves them there.</summary>
sealed class CutFilesAction() : FileAction("cut", "Move", "", "Ready to move")
{
    protected override void Run(IReadOnlyList<string> inputs) => CopyFilesAction.SetFiles(inputs, move: true);
}

sealed class CopyPathAction() : FileAction("copy-path", "Copy path", "", "Copying path")
{
    // Quoted, one per line, the same as Explorer's "Copy as path".
    protected override void Run(IReadOnlyList<string> inputs) =>
        Clipboard.SetText(string.Join(Environment.NewLine, inputs.Select(p => $"\"{p}\"")));
}

sealed class RevealAction() : FileAction("reveal", "Show", "", "Showing in File Explorer")
{
    protected override void Run(IReadOnlyList<string> inputs) => HudWindow.Reveal(inputs[0]);
}

sealed class TerminalAction() : FileAction("terminal", "Terminal", "", "Opening Terminal")
{
    protected override void Run(IReadOnlyList<string> inputs)
    {
        var folder = FolderOf(inputs[0]);
        try
        {
            // Windows Terminal, through its app execution alias.
            Process.Start(new ProcessStartInfo("wt.exe") { ArgumentList = { "-d", folder }, UseShellExecute = false });
        }
        catch (Win32Exception)
        {
            Process.Start(new ProcessStartInfo("powershell.exe") { WorkingDirectory = folder, UseShellExecute = true });
        }
    }
}

/// <summary>Parks the files in a small always-on-top shelf to drag out later.</summary>
sealed class FloatAction() : FileAction("float", "Float", "", "Floating")
{
    protected override void Run(IReadOnlyList<string> inputs) => new FloatWindow(inputs).ShowNearCursor();
}

/// <summary>Opens the Windows share sheet (Nearby Sharing, Mail, Teams and so on).</summary>
sealed class ShareAction(HudWindow hud) : FileAction("share", "Share", "", "Sharing")
{
    public override async Task<IReadOnlyList<string>> RunAsync(JobContext context)
    {
        await ShareSheet.ShowAsync(context.Inputs, hud.Handle);
        return [];
    }
}
