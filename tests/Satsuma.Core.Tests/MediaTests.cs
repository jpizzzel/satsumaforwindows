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

    string OddClip(string name, string size = "161x91") =>
        _files.Media(name, "-f", "lavfi", "-i", $"testsrc=duration=0.4:size={size}:rate=5",
            "-f", "lavfi", "-i", "sine=duration=0.4", "-shortest", "-c:v", "libx264", "-pix_fmt", "yuv444p");

    [Theory]
    [InlineData(WheelMode.Convert, "MP4")]
    [InlineData(WheelMode.Convert, "MOV")]
    [InlineData(WheelMode.Tools, "Compress")]
    [InlineData(WheelMode.Tools, "Rotate")]
    [InlineData(WheelMode.Tools, "720p")]
    public async Task H264_outputs_handle_odd_frame_sizes(WheelMode mode, string label)
    {
        var output = await Run(mode, label, OddClip("odd.mkv"));

        var size = Probe(output, "stream=width,height").Split('\n')[0].Split(',').Select(int.Parse).ToArray();
        Assert.All(size, d => Assert.Equal(0, d % 2));
    }

    [Theory]
    [InlineData("1600x900", "1280,720")]
    [InlineData("900x1600", "720,1280")]
    [InlineData("481x855", "480,854")]
    [InlineData("320x240", "320,240")]
    public async Task To_720p_shrinks_the_short_side_and_never_upscales(string size, string expected)
    {
        var input = _files.Media("in.mp4", "-f", "lavfi", "-i", $"testsrc=duration=0.2:size={size}:rate=5",
            "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv444p");

        var output = await Run(WheelMode.Tools, "720p", input);

        Assert.Equal(expected, Probe(output, "stream=width,height"));
    }

    [Fact]
    public async Task Normalize_keeps_a_normal_sample_rate()
    {
        var output = await Run(WheelMode.Tools, "Normalize", Tone("tone.wav"));

        Assert.Equal("48000", Probe(output, "stream=sample_rate"));
    }

    [Fact]
    public async Task Opus_accepts_side_surround_audio()
    {
        var input = _files.Media("surround.wav", "-f", "lavfi", "-i", "sine=duration=0.5",
            "-af", "pan=5.1(side)|c0=c0|c1=c0|c2=c0|c3=c0|c4=c0|c5=c0");

        var output = await Run(WheelMode.Convert, "OPUS", input);

        Assert.Equal("6", Probe(output, "stream=channels"));
    }

    [Fact]
    public async Task Silent_video_to_audio_says_there_is_no_audio()
    {
        var input = _files.Media("silent.mp4", "-f", "lavfi", "-i", "testsrc=duration=0.4:size=64x48:rate=5",
            "-pix_fmt", "yuv420p");

        var error = await Assert.ThrowsAsync<FfmpegException>(() => Run(WheelMode.Convert, "MP3", input));

        Assert.Equal("This video has no audio track.", error.Message);
        Assert.False(File.Exists(_files.PathOf("silent.mp3")));
    }

    [Fact]
    public async Task Cancelling_stops_ffmpeg_and_leaves_no_output()
    {
        var input = _files.Media("long.mp4", "-f", "lavfi", "-i", "testsrc=duration=20:size=320x240:rate=25",
            "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p");
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var operation = _catalog.For(WheelMode.Convert, [input]).Single(o => o.Label == "WEBM");
        var context = new JobContext { Inputs = [input], Output = new OutputLocator(), Cancellation = cancel.Token };

        cancel.CancelAfter(500);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.RunAsync(context));

        Assert.False(File.Exists(_files.PathOf("long.webm")));
    }
}
