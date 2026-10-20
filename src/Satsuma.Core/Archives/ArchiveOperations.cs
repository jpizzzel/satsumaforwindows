using System.IO.Compression;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Xz;
using SharpCompress.Readers;

namespace Satsuma.Core.Archives;

/// <summary>Zips whatever was dragged: one file or folder becomes "name.zip", several become "Archive.zip".</summary>
public sealed class ZipOperation : FileOperation
{
    public override string Id => "archive.zip";
    public override string Label => "Zip";
    public override string Glyph => "\uF012";
    public override string Description => "Zipping";

    public override bool AppliesTo(IReadOnlyList<string> inputs) => inputs.Count > 0;

    public override Task<IReadOnlyList<string>> RunAsync(JobContext context) =>
        Task.Run<IReadOnlyList<string>>(() =>
        {
            var inputs = context.Inputs;
            var output = inputs.Count == 1
                ? context.Output.For(inputs[0], "zip")
                : context.Output.Named(inputs[0], "Archive.zip");

            try
            {
                using var zip = ZipFile.Open(output, ZipArchiveMode.Create);
                for (var i = 0; i < inputs.Count; i++)
                {
                    context.Cancellation.ThrowIfCancellationRequested();
                    var input = Path.TrimEndingDirectorySeparator(inputs[i]);
                    if (Directory.Exists(input))
                    {
                        var parent = Path.GetDirectoryName(input)!;
                        foreach (var file in Directory.EnumerateFiles(input, "*", SearchOption.AllDirectories))
                        {
                            context.Cancellation.ThrowIfCancellationRequested();
                            zip.CreateEntryFromFile(file, Path.GetRelativePath(parent, file).Replace('\\', '/'));
                        }
                    }
                    else
                    {
                        zip.CreateEntryFromFile(input, Path.GetFileName(input));
                    }
                    context.Progress?.Report((i + 1.0) / inputs.Count);
                }
            }
            catch
            {
                // A half-written zip looks fine in Explorer but is missing files, so don't leave one behind.
                TryDelete(output);
                throw;
            }
            return [output];
        }, context.Cancellation);

    static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

/// <summary>
/// Extracts zip, 7z, rar and tar archives (plain or compressed) into a new folder beside the archive.
/// A lone .gz, .bz2 or .xz file is just one compressed file, so it comes out as that file.
/// </summary>
public sealed class ExtractOperation : PerFileOperation
{
    public override string Id => "archive.extract";
    public override string Label => "Extract";
    public override string Glyph => "\uE8B7";
    public override string Description => "Extracting";

    public override bool AppliesTo(IReadOnlyList<string> inputs) => FileKinds.All(inputs, FileKind.Archive);

    static bool IsTarball(string input) =>
        Path.GetFileName(input).Contains(".tar.", StringComparison.OrdinalIgnoreCase);

    // Wrap the file in the right decompressor when it's a single compressed file rather than an archive.
    static Stream? SingleFileDecompressor(string input, Stream stream)
    {
        if (IsTarball(input))
            return null;
        return Path.GetExtension(input).ToLowerInvariant() switch
        {
            ".gz" => new GZipStream(stream, CompressionMode.Decompress),
            ".bz2" => BZip2Stream.Create(stream, SharpCompress.Compressors.CompressionMode.Decompress, decompressConcatenated: true),
            ".xz" => new XZStream(stream),
            _ => null,
        };
    }

    protected override Task<string> RunOneAsync(string input, JobContext context, IProgress<double>? progress) =>
        Task.Run(() =>
        {
            using var stream = File.OpenRead(input);
            if (SingleFileDecompressor(input, stream) is { } decompressor)
            {
                using (decompressor)
                    return ExtractSingleFile(input, stream, decompressor, context, progress);
            }

            // "photos.tar.gz" should extract to "photos", not "photos.tar".
            var folder = context.Output.FolderFor(IsTarball(input) ? Path.ChangeExtension(input, null) : input);
            Directory.CreateDirectory(folder);
            try
            {
                if (Path.GetExtension(input).Equals(".7z", StringComparison.OrdinalIgnoreCase))
                {
                    // 7z can't be streamed, so it goes through the random-access archive API.
                    using var archive = ArchiveFactory.OpenArchive(stream);
                    using var reader = archive.ExtractAllEntries();
                    var total = Math.Max(archive.TotalUncompressedSize, 1);
                    long done = 0;
                    ExtractEntries(reader, folder, context, progress,
                        () => (done += reader.Entry.Size) / (double)total);
                }
                else
                {
                    using var reader = ReaderFactory.OpenReader(stream);
                    ExtractEntries(reader, folder, context, progress,
                        () => stream.Position / (double)Math.Max(stream.Length, 1));
                }
            }
            catch
            {
                // Half an archive is worse than none, and the folder is new, so it's safe to remove.
                try { Directory.Delete(folder, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                throw;
            }
            return folder;
        }, context.Cancellation);

    static void ExtractEntries(IReader reader, string folder, JobContext context, IProgress<double>? progress,
        Func<double> fractionDone)
    {
        var options = new ExtractionOptions { ExtractFullPath = true, Overwrite = true };
        while (reader.MoveToNextEntry())
        {
            context.Cancellation.ThrowIfCancellationRequested();
            // The library checks each entry stays inside the folder, so "../" and absolute paths can't escape.
            if (!reader.Entry.IsDirectory)
                reader.WriteEntryToDirectory(folder, options);
            progress?.Report(fractionDone());
        }
    }

    static string ExtractSingleFile(string input, Stream compressed, Stream decompressor, JobContext context,
        IProgress<double>? progress)
    {
        // "data.csv.gz" becomes "data.csv".
        var name = Path.GetFileNameWithoutExtension(input);
        var output = context.Output.Named(input, name.Length == 0 ? "extracted" : name);
        try
        {
            using var target = File.Create(output);
            var buffer = new byte[81920];
            int read;
            while ((read = decompressor.Read(buffer)) > 0)
            {
                context.Cancellation.ThrowIfCancellationRequested();
                target.Write(buffer, 0, read);
                progress?.Report(compressed.Position / (double)Math.Max(compressed.Length, 1));
            }
        }
        catch
        {
            try { File.Delete(output); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            throw;
        }
        return output;
    }
}
