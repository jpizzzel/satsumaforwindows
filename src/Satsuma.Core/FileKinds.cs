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
        return ByExtension.GetValueOrDefault(Path.GetExtension(path), FileKind.Unknown);
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
