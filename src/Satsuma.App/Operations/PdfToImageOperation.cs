using System.IO;
using Satsuma.Core;
using Windows.Data.Pdf;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace Satsuma.App.Operations;

/// <summary>
/// Turns PDF pages into images with the PDF renderer built into Windows (no Ghostscript needed).
/// One page gives you name.png. More than that gives you a folder with an image per page.
/// </summary>
sealed class PdfToImageOperation(string label, string extension) : PerFileOperation
{
    /// <summary>2x the page size in points, so 144 DPI. Sharp enough for screens and slides.</summary>
    const double Scale = 2;

    public override string Id => "pdf.convert." + extension;
    public override string Label => label;
    public override string Description => $"Converting to {label}";

    public override bool AppliesTo(IReadOnlyList<string> inputs) => FileKinds.All(inputs, FileKind.Pdf);

    protected override async Task<string> RunOneAsync(string input, JobContext context, IProgress<double>? progress)
    {
        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(input));
        var document = await PdfDocument.LoadFromFileAsync(file);
        var encoder = extension == "jpg" ? BitmapEncoder.JpegEncoderId : BitmapEncoder.PngEncoderId;

        string result;
        Func<uint, string> pathFor;
        if (document.PageCount == 1)
        {
            result = context.Output.For(input, extension);
            pathFor = _ => result;
        }
        else
        {
            result = context.Output.FolderFor(input, "pages");
            Directory.CreateDirectory(result);
            var name = Path.GetFileNameWithoutExtension(input);
            var digits = document.PageCount.ToString().Length;
            pathFor = i => Path.Combine(result, $"{name} - page {(i + 1).ToString().PadLeft(digits, '0')}.{extension}");
        }

        for (uint i = 0; i < document.PageCount; i++)
        {
            context.Cancellation.ThrowIfCancellationRequested();
            using var page = document.GetPage(i);
            var options = new PdfPageRenderOptions
            {
                DestinationWidth = (uint)Math.Round(page.Size.Width * Scale),
                DestinationHeight = (uint)Math.Round(page.Size.Height * Scale),
                BitmapEncoderId = encoder,
            };
            await using (var stream = File.Create(pathFor(i)))
            using (var output = stream.AsRandomAccessStream())
                await page.RenderToStreamAsync(output, options);
            progress?.Report((i + 1.0) / document.PageCount);
        }
        return result;
    }
}
