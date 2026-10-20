using System.IO.Compression;
using SharpCompress.Archives;
using SharpCompress.Common;
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

            using var zip = ZipFile.Open(output, ZipArchiveMode.Create);
            for (var i = 0; i < inputs.Count; i++)
            {
                context.Cancellation.ThrowIfCancellationRequested();
                var input = Path.TrimEndingDirectorySeparator(inputs[i]);
                if (Directory.Exists(input))
                {
                    var parent = Path.GetDirectoryName(input)!;
                    foreach (var file in Directory.EnumerateFiles(input, "*", SearchOption.AllDirectories))
                        zip.CreateEntryFromFile(file, Path.GetRelativePath(parent, file).Replace('\\', '/'));
                }
                else
                {
                    zip.CreateEntryFromFile(input, Path.GetFileName(input));
                }
                context.Progress?.Report((i + 1.0) / inputs.Count);
            }
            return [output];
        }, context.Cancellation);
}

/// <summary>Extracts zip, 7z, rar, tar and gzip archives into a new folder beside the archive.</summary>
public sealed class ExtractOperation : PerFileOperation
{
    public override string Id => "archive.extract";
    public override string Label => "Extract";
    public override string Glyph => "\uE8B7";
    public override string Description => "Extracting";

    public override bool AppliesTo(IReadOnlyList<string> inputs) => FileKinds.All(inputs, FileKind.Archive);

    protected override Task<string> RunOneAsync(string input, JobContext context, IProgress<double>? progress) =>
        Task.Run(() =>
        {
            // "photos.tar.gz" should extract to "photos", not "photos.tar".
            var isTarball = Path.GetFileName(input).Contains(".tar.", StringComparison.OrdinalIgnoreCase);
            var folder = context.Output.FolderFor(isTarball ? Path.ChangeExtension(input, null) : input);
            Directory.CreateDirectory(folder);

            var options = new ExtractionOptions { ExtractFullPath = true, Overwrite = true };
            if (Path.GetExtension(input).Equals(".7z", StringComparison.OrdinalIgnoreCase))
            {
                // 7z can't be streamed, so it goes through the random-access archive API.
                using var archive = ArchiveFactory.OpenArchive(input);
                archive.WriteToDirectory(folder, options);
            }
            else
            {
                using var stream = File.OpenRead(input);
                using var reader = ReaderFactory.OpenReader(stream);
                reader.WriteAllToDirectory(folder, options);
            }
            return folder;
        }, context.Cancellation);
}
