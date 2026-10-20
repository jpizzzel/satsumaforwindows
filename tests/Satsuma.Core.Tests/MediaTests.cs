using System.Diagnostics;
using Satsuma.Core.Media;

namespace Satsuma.Core.Tests;

public sealed class MediaTests : IDisposable
{
    readonly TestFiles _files = new();
    readonly OperationCatalog _catalog = OperationCatalog.CreateDefault();

    public MediaTests() => Assert.SkipWhen(Ffmpeg.Locate() is null, "ffmpeg is not installed.");

    public void Dispose() => _files.Dispose();

    string Tone(string name) =>
        _files.Media(name, "-f", "lavfi", "-i", "sine=frequency=440:duration=2");

    string Clip(string name) =>
        _files.Media(name, "-f", "lavfi", "-i", "testsrc=duration=2:size=320x240:rate=15",
            "-f", "lavfi", "-i", "sine=frequency=440:duration=2", "-shortest", "-pix_fmt", "yuv420p");

    async Task<string> Run(WheelMode mode, string label, string input)
    {
        var operation = _catalog.For(mode, [input]).Single(o => o.Label == label);
        return Assert.Single(await operation.RunAsync(_files.Context(input)));
    }

    static string Probe(string path, string entries)
    {
        var start = new ProcessStartInfo(Path.Combine(Path.GetDirectoryName(Ffmpeg.Locate())!,
            OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe"))
        { RedirectStandardOutput = true };
        foreach (var arg in (string[])["-v", "error", "-show_entries", entries, "-of", "csv=p=0", path])
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return output;
    }

    [Fact]
    public async Task Wav_converts_to_mp3()
    {
        var output = await Run(WheelMode.Convert, "MP3", Tone("tone.wav"));

        Assert.EndsWith("tone.mp3", output);
        Assert.Equal("mp3", Probe(output, "stream=codec_name"));
    }

    [Fact]
    public async Task Video_converts_to_audio_only()
    {
        var output = await Run(WheelMode.Convert, "M4A", Clip("clip.mp4"));

        Assert.Equal("aac", Probe(output, "stream=codec_name"));
    }

    [Fact]
    public async Task Video_converts_to_mov_with_progress()
    {
        var input = Clip("clip.mp4");
        var reports = new List<double>();
        var operation = _catalog.For(WheelMode.Convert, [input]).Single(o => o.Label == "MOV");
        var context = new JobContext
        {
            Inputs = [input],
            Output = new OutputLocator(),
            Progress = new Progress<double>(reports.Add),
            Cancellation = TestContext.Current.CancellationToken,
        };

        var output = Assert.Single(await operation.RunAsync(context));
        await Task.Delay(100, TestContext.Current.CancellationToken); // Progress<T> posts asynchronously.

        Assert.EndsWith("clip.mov", output);
        Assert.Contains("h264", Probe(output, "stream=codec_name"));
        Assert.NotEmpty(reports);
    }

    [Fact]
    public async Task Video_becomes_a_gif()
    {
        var output = await Run(WheelMode.Convert, "GIF", Clip("clip.mp4"));

        Assert.Equal("gif", Probe(output, "stream=codec_name"));
    }

    [Fact]
    public async Task Mute_drops_the_audio_track()
    {
        var output = await Run(WheelMode.Tools, "Mute", Clip("clip.mp4"));

        Assert.EndsWith("clip (muted).mp4", output);
        Assert.Equal("video", Probe(output, "stream=codec_type"));
    }

    [Fact]
    public async Task Subtitles_convert_between_formats()
    {
        var input = _files.PathOf("talk.srt");
        await File.WriteAllTextAsync(input, "1\n00:00:01,000 --> 00:00:02,500\nHello there\n\n",
            TestContext.Current.CancellationToken);

        var output = await Run(WheelMode.Convert, "VTT", input);

        var text = await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken);
        Assert.StartsWith("WEBVTT", text);
        Assert.Contains("Hello there", text);
    }

    [Fact]
    public async Task Broken_input_reports_an_ffmpeg_error_and_leaves_no_output()
    {
        var input = _files.PathOf("broken.mp4");
        await File.WriteAllTextAsync(input, "not a video", TestContext.Current.CancellationToken);

        var operation = _catalog.For(WheelMode.Convert, [input]).Single(o => o.Label == "MOV");
        await Assert.ThrowsAsync<FfmpegException>(() => operation.RunAsync(_files.Context(input)));
        Assert.False(File.Exists(_files.PathOf("broken.mov")));
    }
}
