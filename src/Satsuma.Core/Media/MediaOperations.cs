namespace Satsuma.Core.Media;

/// <summary>Any conversion or edit that boils down to one ffmpeg call per file.</summary>
public sealed class FfmpegOperation : PerFileOperation
{
    public required string OperationId { get; init; }
    public required string Name { get; init; }
    public required string Verb { get; init; }
    public string? Icon { get; init; }
    public required FileKind[] Accepts { get; init; }

    /// <summary>Output extension; null keeps the input's own extension.</summary>
    public string? Extension { get; init; }

    /// <summary>Added to the output name for edits, e.g. "clip (muted).mp4".</summary>
    public string? Suffix { get; init; }

    public required string[] Arguments { get; init; }

    public override string Id => OperationId;
    public override string Label => Name;
    public override string? Glyph => Icon;
    public override string Description => Verb;

    public override bool AppliesTo(IReadOnlyList<string> inputs) =>
        FileKinds.All(inputs, Accepts)
        && (Suffix is not null || Extension is null
            || !inputs.All(i => FileKinds.SameFormat(Path.GetExtension(i), Extension)));

    protected override async Task<string> RunOneAsync(string input, JobContext context, IProgress<double>? progress)
    {
        var output = context.Output.For(input, Extension ?? Path.GetExtension(input), Suffix);
        await Ffmpeg.RunAsync(input, Arguments, output, progress, context.Cancellation);
        return output;
    }
}

public static class MediaOperations
{
    static readonly FileKind[] Video = [FileKind.Video];
    static readonly FileKind[] Audio = [FileKind.Audio];
    static readonly FileKind[] AudioOrVideo = [FileKind.Audio, FileKind.Video];
    static readonly FileKind[] Subtitles = [FileKind.Subtitle];

    const string H264 = "-c:v libx264 -preset medium -crf 23 -pix_fmt yuv420p";
    const string Aac = "-c:a aac -b:a 192k";

    static FfmpegOperation Convert(string label, string extension, FileKind[] accepts, string arguments) => new()
    {
        OperationId = "media.convert." + extension,
        Name = label,
        Verb = $"Converting to {label}",
        Accepts = accepts,
        Extension = extension,
        Arguments = Split(arguments),
    };

    static FfmpegOperation Edit(string id, string label, string verb, string glyph, FileKind[] accepts,
        string suffix, string arguments, string? extension = null) => new()
    {
        OperationId = id,
        Name = label,
        Verb = verb,
        Icon = glyph,
        Accepts = accepts,
        Suffix = suffix,
        Extension = extension,
        Arguments = Split(arguments),
    };

    // Filter graphs go in as a single argument so ffmpeg gets their commas and quotes as-is.
    static string[] Split(string arguments) => arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    public static IEnumerable<FileOperation> Conversions() =>
    [
        Convert("MP4", "mp4", Video, $"{H264} {Aac} -movflags +faststart"),
        Convert("MOV", "mov", Video, $"{H264} {Aac}"),
        Convert("MKV", "mkv", Video, $"{H264} {Aac}"),
        Convert("WEBM", "webm", Video, "-c:v libvpx-vp9 -crf 32 -b:v 0 -row-mt 1 -deadline good -cpu-used 4 -c:a libopus -b:a 128k"),
        Convert("AVI", "avi", Video, "-c:v mpeg4 -q:v 3 -c:a libmp3lame -q:a 2"),
        new FfmpegOperation
        {
            OperationId = "media.convert.gif",
            Name = "GIF",
            Verb = "Making a GIF",
            Accepts = Video,
            Extension = "gif",
            Arguments = ["-an", "-vf", "fps=12,scale='min(640,iw)':-2:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse", "-loop", "0"],
        },
        Convert("MP3", "mp3", AudioOrVideo, "-vn -c:a libmp3lame -q:a 2"),
        Convert("WAV", "wav", AudioOrVideo, "-vn -c:a pcm_s16le"),
        Convert("M4A", "m4a", AudioOrVideo, $"-vn {Aac}"),
        Convert("FLAC", "flac", AudioOrVideo, "-vn -c:a flac"),
        Convert("OGG", "ogg", AudioOrVideo, "-vn -c:a libvorbis -q:a 5"),
        Convert("OPUS", "opus", AudioOrVideo, "-vn -c:a libopus -b:a 128k"),
        Convert("AIFF", "aiff", AudioOrVideo, "-vn -c:a pcm_s16be"),
        Convert("SRT", "srt", Subtitles, ""),
        Convert("VTT", "vtt", Subtitles, ""),
        Convert("ASS", "ass", Subtitles, ""),
    ];

    public static IEnumerable<FileOperation> Tools() =>
    [
        Edit("video.compress", "Compress", "Compressing", "\uE73F", Video, "compressed",
            "-c:v libx264 -preset slow -crf 28 -pix_fmt yuv420p -c:a aac -b:a 128k -movflags +faststart", "mp4"),
        new FfmpegOperation
        {
            OperationId = "video.720p",
            Name = "720p",
            Verb = "Resizing to 720p",
            Icon = "\uE740",
            Accepts = Video,
            Suffix = "720p",
            Extension = "mp4",
            Arguments = ["-vf", "scale=-2:'min(720,ih)'", .. Split($"{H264} {Aac} -movflags +faststart")],
        },
        Edit("video.mute", "Mute", "Removing audio", "\uE74F", Video, "muted", "-map 0:v -c copy -an"),
        new FfmpegOperation
        {
            OperationId = "video.rotate",
            Name = "Rotate",
            Verb = "Rotating",
            Icon = "\uE7AD",
            Accepts = Video,
            Suffix = "rotated",
            Extension = "mp4",
            Arguments = ["-vf", "transpose=clock", .. Split($"{H264} {Aac} -movflags +faststart")],
        },
        Edit("audio.compress", "Compress", "Compressing", "\uE73F", Audio, "compressed",
            "-vn -c:a libmp3lame -b:a 96k", "mp3"),
        new FfmpegOperation
        {
            OperationId = "audio.normalize",
            Name = "Normalize",
            Verb = "Normalizing loudness",
            Icon = "\uE767",
            Accepts = Audio,
            Suffix = "normalized",
            Arguments = ["-vn", "-af", "loudnorm=I=-16:TP=-1.5:LRA=11"],
        },
        new FfmpegOperation
        {
            OperationId = "audio.trim-silence",
            Name = "Trim silence",
            Verb = "Trimming silence",
            Icon = "\uE7A8",
            Accepts = Audio,
            Suffix = "trimmed",
            // Trim the start, reverse, trim again (really the end), reverse back.
            Arguments = ["-vn", "-af", "silenceremove=start_periods=1:start_threshold=-50dB,areverse,silenceremove=start_periods=1:start_threshold=-50dB,areverse"],
        },
    ];
}
