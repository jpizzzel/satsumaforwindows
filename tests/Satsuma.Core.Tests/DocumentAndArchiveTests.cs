using System.Formats.Tar;
using System.IO.Compression;
using PdfSharp.Pdf.IO;
using Satsuma.Core.Archives;
using Satsuma.Core.Documents;

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
            // Refusing the whole archive is fine too; what matters is that nothing lands outside.
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
}
