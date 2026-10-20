using ImageMagick;
using Satsuma.Core.Images;

namespace Satsuma.Core.Tests;

public sealed class ImageTests : IDisposable
{
    readonly TestFiles _files = new();

    public void Dispose() => _files.Dispose();

    async Task<string> RunSingle(FileOperation operation, params string[] inputs)
    {
        Assert.True(operation.AppliesTo(inputs));
        var outputs = await operation.RunAsync(_files.Context(inputs));
        return Assert.Single(outputs);
    }

    [Theory]
    [InlineData("JPG", "jpg", MagickFormat.Jpeg)]
    [InlineData("WEBP", "webp", MagickFormat.WebP)]
    [InlineData("AVIF", "avif", MagickFormat.Avif)]
    [InlineData("HEIC", "heic", MagickFormat.Heic)]
    [InlineData("TIFF", "tiff", MagickFormat.Tiff)]
    [InlineData("ICO", "ico", MagickFormat.Icon)]
    [InlineData("PDF", "pdf", MagickFormat.Pdf)]
    public async Task Converts_png_to(string label, string extension, MagickFormat format)
    {
        Assert.SkipUnless(ImageConvertOperation.CanWrite(format), $"This Magick.NET build cannot write {label}.");
        var input = _files.Image("photo.png");

        var output = await RunSingle(new ImageConvertOperation(label, extension, format), input);

        Assert.Equal(_files.PathOf($"photo.{extension}"), output);
        if (format == MagickFormat.Icon)
        {
            using var sizes = new MagickImageCollection(output);
            Assert.Equal(MagickFormat.Ico, sizes[0].Format);
            Assert.True(sizes.Count > 1, "An icon should hold several sizes.");
        }
        else if (format != MagickFormat.Pdf)
        {
            using var result = new MagickImage(output);
            Assert.Equal(format, result.Format);
        }
        else
        {
            Assert.StartsWith("%PDF", File.ReadAllText(output)[..4]);
        }
    }

    [Fact]
    public async Task Transparent_png_becomes_jpg_on_white()
    {
        var input = _files.Image("logo.png", transparent: true);

        var output = await RunSingle(new ImageConvertOperation("JPG", "jpg", MagickFormat.Jpeg), input);

        using var result = new MagickImage(output);
        var corner = result.GetPixels().GetPixel(63, 31).ToColor()!;
        Assert.True(corner.R > 245 && corner.G > 245 && corner.B > 245, $"Expected white, got {corner}");
    }

    [Fact]
    public async Task Animated_gif_keeps_its_frames_as_webp()
    {
        var input = _files.AnimatedGif("spin.gif");

        var output = await RunSingle(new ImageConvertOperation("WEBP", "webp", MagickFormat.WebP), input);

        using var frames = new MagickImageCollection(output);
        Assert.Equal(3, frames.Count);
    }

    [Fact]
    public async Task Resize_halves_dimensions()
    {
        var output = await RunSingle(new ImageResizeOperation(50), _files.Image("big.png", 200, 100));

        Assert.EndsWith("big (50%).png", output);
        using var result = new MagickImage(output);
        Assert.Equal((100u, 50u), (result.Width, result.Height));
    }

    [Fact]
    public async Task Rotate_swaps_dimensions()
    {
        var output = await RunSingle(new ImageRotateOperation(), _files.Image("wide.jpg", 80, 20));

        using var result = new MagickImage(output);
        Assert.Equal((20u, 80u), (result.Width, result.Height));
    }

    [Fact]
    public async Task Strip_metadata_removes_exif()
    {
        var input = _files.PathOf("camera.jpg");
        using (var image = new MagickImage(MagickColors.Gray, 40, 40))
        {
            var exif = new ExifProfile();
            exif.SetValue(ExifTag.Make, "Satsuma Test Camera");
            image.SetProfile(exif);
            image.Write(input);
        }

        var output = await RunSingle(new ImageStripMetadataOperation(), input);

        using var result = new MagickImage(output);
        Assert.Null(result.GetExifProfile());
    }

    [Fact]
    public async Task Compress_makes_a_photo_smaller()
    {
        var input = _files.PathOf("noisy.jpg");
        using (var image = new MagickImage("plasma:", 400, 300))
        {
            image.Quality = 100;
            image.Write(input);
        }

        var output = await RunSingle(new ImageCompressOperation(), input);

        Assert.True(new FileInfo(output).Length < new FileInfo(input).Length);
    }

    [Fact]
    public async Task Several_images_combine_into_one_pdf()
    {
        var a = _files.Image("a.png");
        var b = _files.Image("b.jpg");

        var output = await RunSingle(new ImagesToPdfOperation(), a, b);

        Assert.EndsWith("a (combined).pdf", output);
        using var document = PdfSharp.Pdf.IO.PdfReader.Open(output, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
        Assert.Equal(2, document.PageCount);
    }

    [Fact]
    public async Task Converting_several_files_reports_progress_to_completion()
    {
        var inputs = new[] { _files.Image("1.png"), _files.Image("2.png"), _files.Image("3.png") };
        var reports = new List<double>();
        var context = new JobContext
        {
            Inputs = inputs,
            Output = new OutputLocator(),
            Progress = new SynchronousProgress(reports.Add),
            Cancellation = TestContext.Current.CancellationToken,
        };

        var outputs = await new ImageConvertOperation("JPG", "jpg", MagickFormat.Jpeg).RunAsync(context);

        Assert.Equal(3, outputs.Count);
        Assert.Equal(1.0, reports[^1]);
        Assert.Equal(reports.Order(), reports);
    }

    sealed class SynchronousProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
