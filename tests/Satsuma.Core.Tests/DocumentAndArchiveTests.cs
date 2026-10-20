using System.Formats.Tar;
using System.IO.Compression;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Satsuma.Core.Archives;
using Satsuma.Core.Documents;
using SharpCompress.Common;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Writers;
using SharpCompress.Writers.SevenZip;

namespace Satsuma.Core.Tests;

public sealed class DocumentAndArchiveTests : IDisposable
{
    readonly TestFiles _files = new();

    public void Dispose() => _files.Dispose();

    static int PageCount(string path)
    {
        using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        return document.PageCount;
    }

    [Fact]
    public async Task Merge_joins_pages_in_drag_order()
    {
        var a = _files.Pdf("a.pdf", 2);
        var b = _files.Pdf("b.pdf", 3);

        var output = Assert.Single(await new PdfMergeOperation().RunAsync(_files.Context(a, b)));

        Assert.EndsWith("a (merged).pdf", output);
        Assert.Equal(5, PageCount(output));
    }

    [Fact]
    public async Task Split_writes_one_pdf_per_page()
    {
        var input = _files.Pdf("report.pdf", 3);

        var folder = Assert.Single(await new PdfSplitOperation().RunAsync(_files.Context(input)));

        var pages = Directory.GetFiles(folder).Select(Path.GetFileName).Order().ToList();
        Assert.Equal(["report - page 1.pdf", "report - page 2.pdf", "report - page 3.pdf"], pages);
        Assert.All(Directory.GetFiles(folder), p => Assert.Equal(1, PageCount(p)));
    }

    [Fact]
    public async Task Rotate_turns_every_page()
    {
        var output = Assert.Single(await new PdfRotateOperation().RunAsync(_files.Context(_files.Pdf("scan.pdf", 2))));

        using var document = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.All(document.Pages.Cast<PdfSharp.Pdf.PdfPage>(), p => Assert.Equal(90, p.Rotate));
    }

    [Fact]
    public async Task Zip_includes_files_and_folders()
    {
        var folder = Directory.CreateDirectory(_files.PathOf("album")).FullName;
        File.WriteAllText(Path.Combine(folder, "track.txt"), "1");
        Directory.CreateDirectory(Path.Combine(folder, "art"));
        File.WriteAllText(Path.Combine(folder, "art", "cover.txt"), "2");
        var note = _files.PathOf("note.txt");
        File.WriteAllText(note, "3");

        var output = Assert.Single(await new ZipOperation().RunAsync(_files.Context(folder, note)));

        Assert.EndsWith("Archive.zip", output);
        using var zip = ZipFile.OpenRead(output);
        Assert.Equal(["album/art/cover.txt", "album/track.txt", "note.txt"],
            zip.Entries.Select(e => e.FullName).Order());
    }

    [Fact]
    public async Task Extract_unpacks_zip_beside_the_archive()
    {
        var archive = _files.PathOf("bundle.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(zip.CreateEntry("docs/readme.txt").Open());
            writer.Write("hi");
        }

        var folder = Assert.Single(await new ExtractOperation().RunAsync(_files.Context(archive)));

        Assert.Equal(_files.PathOf("bundle"), folder);
        Assert.Equal("hi", File.ReadAllText(Path.Combine(folder, "docs", "readme.txt")));
    }

    [Fact]
    public async Task Extract_refuses_entries_that_escape_the_folder()
    {
        var archive = _files.PathOf("evil.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(zip.CreateEntry("../escaped.txt").Open());
            writer.Write("gotcha");
        }

        try
        {
            await new ExtractOperation().RunAsync(_files.Context(archive));
        }
        catch (Exception)
        {
            // Throwing on the whole archive is fine too. All we care about is nothing ending up outside.
        }

        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(_files.Root)!, "escaped.txt")));
        Assert.False(File.Exists(_files.PathOf("escaped.txt")));
    }

    [Fact]
    public async Task Extract_names_tarball_folders_without_tar()
    {
        var source = Directory.CreateDirectory(_files.PathOf("src")).FullName;
        File.WriteAllText(Path.Combine(source, "a.txt"), "tar!");
        var archive = _files.PathOf("photos.tar.gz");
        using (var gz = new GZipStream(File.Create(archive), CompressionLevel.Fastest))
            TarFile.CreateFromDirectory(source, gz, includeBaseDirectory: false);

        var folder = Assert.Single(await new ExtractOperation().RunAsync(_files.Context(archive)));

        Assert.Equal(_files.PathOf("photos"), folder);
        Assert.Equal("tar!", File.ReadAllText(Path.Combine(folder, "a.txt")));
    }

    sealed class SynchronousProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }

    [Fact]
    public async Task Rotate_works_on_pdfs_locked_against_editing()
    {
        var input = _files.PathOf("locked.pdf");
        using (var document = new PdfDocument())
        {
            document.AddPage();
            document.AddPage();
            document.SecuritySettings.OwnerPassword = "owner";
            document.SecuritySettings.PermitModifyDocument = false;
            document.Save(input);
        }

        var output = Assert.Single(await new PdfRotateOperation().RunAsync(_files.Context(input)));

        using var result = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Equal(2, result.PageCount);
        Assert.All(result.Pages.Cast<PdfPage>(), p => Assert.Equal(90, p.Rotate));
    }

    string Folder(string name, int files)
    {
        var folder = Directory.CreateDirectory(_files.PathOf(name)).FullName;
        for (var i = 0; i < files; i++)
            File.WriteAllText(Path.Combine(folder, $"{i}.txt"), new string('x', 1000));
        return folder;
    }

    [Fact]
    public async Task Cancelled_zip_leaves_nothing_behind()
    {
        var a = Folder("a", 3);
        var b = Folder("b", 3);
        using var cancel = new CancellationTokenSource();
        var context = new JobContext
        {
            Inputs = [a, b],
            Output = new OutputLocator(),
            Progress = new SynchronousProgress(_ => cancel.Cancel()),
            Cancellation = cancel.Token,
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ZipOperation().RunAsync(context));

        Assert.False(File.Exists(_files.PathOf("Archive.zip")));
    }

    [Fact]
    public async Task Failed_zip_leaves_nothing_behind()
    {
        var note = _files.PathOf("note.txt");
        File.WriteAllText(note, "hi");

        await Assert.ThrowsAnyAsync<IOException>(() =>
            new ZipOperation().RunAsync(_files.Context(note, _files.PathOf("gone.txt"))));

        Assert.False(File.Exists(_files.PathOf("Archive.zip")));
    }

    [Fact]
    public async Task Cancelled_extract_removes_the_half_done_folder()
    {
        var archive = _files.PathOf("many.zip");
        ZipFile.CreateFromDirectory(Folder("many", 5), archive);
        using var cancel = new CancellationTokenSource();
        var context = new JobContext
        {
            Inputs = [archive],
            Output = new OutputLocator(),
            Progress = new SynchronousProgress(_ => cancel.Cancel()),
            Cancellation = cancel.Token,
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ExtractOperation().RunAsync(context));

        Assert.False(Directory.Exists(_files.PathOf("many (1)")));
    }

    [Fact]
    public async Task Extract_refuses_absolute_paths_in_tarballs()
    {
        var target = Path.Combine(Path.GetDirectoryName(_files.Root)!, $"satsuma-escape-{Guid.NewGuid():N}.txt");
        var archive = _files.PathOf("abs.tar");
        using (var tar = new TarWriter(File.Create(archive)))
        {
            var entry = new PaxTarEntry(TarEntryType.RegularFile, target) { DataStream = new MemoryStream("gotcha"u8.ToArray()) };
            tar.WriteEntry(entry);
        }

        try
        {
            await new ExtractOperation().RunAsync(_files.Context(archive));
        }
        catch (Exception)
        {
            // Throwing is fine, as long as nothing lands outside.
        }

        Assert.False(File.Exists(target));
    }

    [Theory]
    [InlineData("gz")]
    [InlineData("bz2")]
    [InlineData("xz")]
    public async Task Single_compressed_files_extract_to_the_file_inside(string extension)
    {
        var archive = _files.PathOf($"data.csv.{extension}");
        var text = "a,b\n"u8.ToArray();
        switch (extension)
        {
            case "gz":
                using (var gz = new GZipStream(File.Create(archive), CompressionLevel.Fastest))
                    gz.Write(text);
                break;
            case "bz2":
                using (var bz = BZip2Stream.Create(File.Create(archive), SharpCompress.Compressors.CompressionMode.Compress, false))
                    bz.Write(text);
                break;
            case "xz":
                // "a,b\n" made with Python's lzma.compress, since there's no xz writer here.
                File.WriteAllBytes(archive, Convert.FromBase64String(
                    "/Td6WFoAAATm1rRGAgAhARYAAAB0L+WjAQADYSxiCgAPPyrsxd0zFgABHARvLJzBH7bzfQEAAAAABFla"));
                break;
        }

        var output = Assert.Single(await new ExtractOperation().RunAsync(_files.Context(archive)));

        Assert.Equal(_files.PathOf("data.csv"), output);
        Assert.Equal(text, File.ReadAllBytes(output));
    }

    [Fact]
    public async Task Extract_unpacks_7z()
    {
        var archive = _files.PathOf("bundle.7z");
        using (var stream = File.Create(archive))
        using (var writer = WriterFactory.OpenWriter(stream, ArchiveType.SevenZip, new SevenZipWriterOptions()))
            writer.Write("docs/readme.txt", new MemoryStream("hi"u8.ToArray()));

        var folder = Assert.Single(await new ExtractOperation().RunAsync(_files.Context(archive)));

        Assert.Equal("hi", File.ReadAllText(Path.Combine(folder, "docs", "readme.txt")));
    }
}
