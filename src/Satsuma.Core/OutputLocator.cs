namespace Satsuma.Core;

/// <summary>Decides where results go and keeps them from overwriting existing files.</summary>
public sealed class OutputLocator
{
    /// <summary>When set, results go here instead of next to the original.</summary>
    public string? Folder { get; init; }

    /// <summary>
    /// Path for a result derived from <paramref name="input"/>, e.g. "photo.heic" becomes "photo.png" or,
    /// with a suffix, "photo (compressed).jpg". Adds " (1)", " (2)" and so on if the name is taken.
    /// </summary>
    public string For(string input, string extension, string? suffix = null)
    {
        var name = Stem(input);
        if (suffix is not null)
            name += $" ({suffix})";
        extension = extension.TrimStart('.');
        return Unique(Path.Combine(DirectoryFor(input), name), extension.Length == 0 ? "" : "." + extension);
    }

    /// <summary>Same file or folder name in the output folder, made unique. Used when copying or moving.</summary>
    public string SameName(string input)
    {
        input = Path.TrimEndingDirectorySeparator(input);
        if (Directory.Exists(input))
            return Unique(Path.Combine(DirectoryFor(input), Path.GetFileName(input)), "");
        return Unique(Path.Combine(DirectoryFor(input), Path.GetFileNameWithoutExtension(input)), Path.GetExtension(input));
    }

    /// <summary>Path for a result built from several inputs, e.g. merged PDFs.</summary>
    public string Named(string nearInput, string fileName)
    {
        return Unique(Path.Combine(DirectoryFor(nearInput), Path.GetFileNameWithoutExtension(fileName)),
            Path.GetExtension(fileName));
    }

    /// <summary>A new folder path for results that produce many files.</summary>
    public string FolderFor(string input, string? suffix = null)
    {
        var name = Stem(input);
        if (suffix is not null)
            name += $" ({suffix})";
        return Unique(Path.Combine(DirectoryFor(input), name), "");
    }

    // Folders keep their whole name ("release.v1.2"), and so do dotfiles like ".env".
    static string Stem(string input)
    {
        input = Path.TrimEndingDirectorySeparator(input);
        var name = Path.GetFileName(input);
        if (Directory.Exists(input))
            return name;
        var stem = Path.GetFileNameWithoutExtension(input);
        return stem.Length == 0 ? name : stem;
    }

    string DirectoryFor(string input)
    {
        if (Folder is not null)
        {
            Directory.CreateDirectory(Folder);
            return Folder;
        }
        return Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(input)))
            ?? throw new ArgumentException($"Cannot find the folder of {input}", nameof(input));
    }

    static string Unique(string pathWithoutExtension, string extension)
    {
        var candidate = pathWithoutExtension + extension;
        for (var n = 1; File.Exists(candidate) || Directory.Exists(candidate); n++)
            candidate = $"{pathWithoutExtension} ({n}){extension}";
        return candidate;
    }
}
