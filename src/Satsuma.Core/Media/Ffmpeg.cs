using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Satsuma.Core.Media;

public sealed class FfmpegException(string message) : Exception(message)
{
    /// <summary>The last lines ffmpeg wrote to stderr, when there are any.</summary>
    public string ErrorOutput { get; init; } = "";
}

public static partial class Ffmpeg
{
    /// <summary>Lets tests and power users point at a specific ffmpeg build.</summary>
    public const string PathVariable = "SATSUMA_FFMPEG";

    static readonly string ExeName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

    /// <summary>
    /// Looks for ffmpeg in this order: the SATSUMA_FFMPEG variable, tools\ffmpeg.exe next to the app (where
    /// the release puts it), the app folder, then PATH.
    /// </summary>
    public static string? Locate()
    {
        var fromVariable = Environment.GetEnvironmentVariable(PathVariable);
        if (!string.IsNullOrEmpty(fromVariable) && File.Exists(fromVariable))
            return fromVariable;

        string[] beside = [Path.Combine(AppContext.BaseDirectory, "tools", ExeName), Path.Combine(AppContext.BaseDirectory, ExeName)];
        foreach (var candidate in beside)
            if (File.Exists(candidate))
                return candidate;

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (dir.Length == 0)
                continue;
            var candidate = Path.Combine(dir.Trim('"'), ExeName);
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    public static async Task RunAsync(string input, IEnumerable<string> outputArgs, string output,
        IProgress<double>? progress, CancellationToken cancellation)
    {
        var exe = Locate() ?? throw new FfmpegException(
            "ffmpeg was not found. Put ffmpeg.exe in the 'tools' folder next to Satsuma, or add it to PATH.");

        var start = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in (string[])["-hide_banner", "-nostdin", "-y", "-progress", "pipe:1", "-nostats", "-i", input])
            start.ArgumentList.Add(arg);
        foreach (var arg in outputArgs)
            start.ArgumentList.Add(arg);
        start.ArgumentList.Add(output);

        using var process = Process.Start(start) ?? throw new FfmpegException("Could not start ffmpeg.");
        TimeSpan? duration = null;
        var errorTail = new Queue<string>();

        var readErrors = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync() is { } line)
            {
                if (duration is null && DurationLine().Match(line) is { Success: true } m
                    && TimeSpan.TryParse(m.Groups[1].Value, CultureInfo.InvariantCulture, out var d) && d > TimeSpan.Zero)
                    duration = d;
                errorTail.Enqueue(line);
                if (errorTail.Count > 12)
                    errorTail.Dequeue();
            }
        });
        var readProgress = Task.Run(async () =>
        {
            while (await process.StandardOutput.ReadLineAsync() is { } line)
            {
                // out_time_us is microseconds. Older builds call it out_time_ms, but it's microseconds there too.
                if (progress is null || duration is not { } total || !line.StartsWith("out_time_", StringComparison.Ordinal))
                    continue;
                var eq = line.IndexOf('=');
                if (line.AsSpan(0, eq) is "out_time_us" or "out_time_ms"
                    && long.TryParse(line.AsSpan(eq + 1), CultureInfo.InvariantCulture, out var us))
                    progress.Report(Math.Clamp(us / 1_000_000.0 / total.TotalSeconds, 0, 1));
            }
        });

        try
        {
            await process.WaitForExitAsync(cancellation);
        }
        catch (OperationCanceledException)
        {
            // Windows keeps the file locked until ffmpeg has really gone, so wait for it before cleaning up.
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { } // Already exited.
            process.WaitForExit(5000);
            try { await Task.WhenAll(readErrors, readProgress).WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception) { } // We're cancelling anyway.
            await TryDeleteAsync(output);
            throw;
        }
        await Task.WhenAll(readErrors, readProgress);

        if (process.ExitCode != 0)
        {
            await TryDeleteAsync(output);
            var reason = errorTail.LastOrDefault(l => l.Trim().Length > 0) ?? $"exit code {process.ExitCode}";
            throw new FfmpegException($"ffmpeg failed: {reason.Trim()}") { ErrorOutput = string.Join('\n', errorTail) };
        }
    }

    static async Task TryDeleteAsync(string path)
    {
        // A virus scanner or the indexer can hold the file for a moment, so give it a few tries.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                await Task.Delay(100);
            }
            catch (IOException) { return; }
            catch (UnauthorizedAccessException) { return; }
        }
    }

    [GeneratedRegex(@"Duration:\s*(\d+:\d{2}:\d{2}(?:\.\d+)?)")]
    private static partial Regex DurationLine();
}
