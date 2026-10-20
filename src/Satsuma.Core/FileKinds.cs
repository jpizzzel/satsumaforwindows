namespace Satsuma.Core;

public enum FileKind
{
    Unknown,
    Folder,
    Image,
    Video,
    Audio,
    Pdf,
    Subtitle,
    Archive,
}

/// <summary>Which wheel the user asked for, chosen by the modifier held during the drag.</summary>
public enum WheelMode
{
    Convert,
    Tools,
    Actions,
}

public static class FileKinds
{
    static readonly Dictionary<string, FileKind> ByExtension = new(StringComparer.OrdinalIgnoreCase);

    static FileKinds()
    {
        Map(FileKind.Image, ".jpg", ".jpeg", ".jfif", ".png", ".webp", ".heic", ".heif", ".avif", ".gif", ".bmp",
            ".tif", ".tiff", ".ico", ".svg", ".psd", ".tga", ".jxl", ".dng", ".cr2", ".nef", ".arw");
        Map(FileKind.Video, ".mp4", ".mov", ".mkv", ".avi", ".webm", ".m4v", ".wmv", ".flv", ".mpg", ".mpeg",
            ".3gp", ".ts", ".mts", ".m2ts");
        // .ts is also TypeScript, so these are only video when the bytes say so. See Of.
        Map(FileKind.Audio, ".mp3", ".wav", ".m4a", ".aac", ".flac", ".ogg", ".oga", ".opus", ".wma", ".aif",
            ".aiff", ".alac", ".amr");
        Map(FileKind.Pdf, ".pdf");
        Map(FileKind.Subtitle, ".srt", ".vtt", ".ass", ".ssa");
        Map(FileKind.Archive, ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".xz");
    }

    static void Map(FileKind kind, params string[] extensions)
    {
        foreach (var ext in extensions)
            ByExtension[ext] = kind;
    }

    public static FileKind Of(string path)
    {
        if (Directory.Exists(path))
            return FileKind.Folder;
        var extension = Path.GetExtension(path);
        if (TransportStreamExtensions.Contains(extension))
            return LooksLikeTransportStream(path) ? FileKind.Video : FileKind.Unknown;
        return ByExtension.GetValueOrDefault(extension, FileKind.Unknown);
    }

    static readonly HashSet<string> TransportStreamExtensions = new(StringComparer.OrdinalIgnoreCase) { ".ts", ".mts", ".m2ts" };

    // MPEG-TS packets are 188 bytes and start with 0x47. Blu-ray and camera files (.m2ts, .mts) add a
    // 4-byte timestamp first, so their packets are 192 bytes. Checking two packets keeps a TypeScript
    // file that happens to start with "G" from looking like video.
    static bool LooksLikeTransportStream(string path)
    {
        const byte Sync = 0x47;
        try
        {
            Span<byte> head = stackalloc byte[197];
            using var stream = File.OpenRead(path);
            var read = stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
            return (read > 188 && head[0] == Sync && head[188] == Sync)
                || (read > 196 && head[4] == Sync && head[196] == Sync);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Can't tell, so don't offer video tools for what might be source code.
            return false;
        }
    }

    /// <summary>True when every path is one of <paramref name="kinds"/>.</summary>
    public static bool All(IReadOnlyList<string> paths, params FileKind[] kinds) =>
        paths.Count > 0 && paths.All(p => kinds.Contains(Of(p)));

    /// <summary>Treats .jpg/.jpeg, .tif/.tiff and so on as the same format.</summary>
    public static bool SameFormat(string extensionA, string extensionB) =>
        Canonical(extensionA) == Canonical(extensionB);

    static string Canonical(string extension) => extension.TrimStart('.').ToLowerInvariant() switch
    {
        "jpeg" or "jfif" => "jpg",
        "tiff" => "tif",
        "heif" => "heic",
        "aif" => "aiff",
        "mpeg" => "mpg",
        "oga" => "ogg",
        var e => e,
    };
}
