using ImageMagick;
using Satsuma.Core.Archives;
using Satsuma.Core.Documents;
using Satsuma.Core.Images;
using Satsuma.Core.Media;

namespace Satsuma.Core;

/// <summary>Everything that can appear on the wheels, in display order.</summary>
public sealed class OperationCatalog
{
    readonly List<(WheelMode Mode, FileOperation Operation)> _entries = [];

    public OperationCatalog Add(WheelMode mode, params IEnumerable<FileOperation> operations)
    {
        foreach (var operation in operations)
            _entries.Add((mode, operation));
        return this;
    }

    public IReadOnlyList<FileOperation> For(WheelMode mode, IReadOnlyList<string> inputs) =>
        _entries.Where(e => e.Mode == mode && e.Operation.AppliesTo(inputs)).Select(e => e.Operation).ToList();

    /// <summary>The platform-neutral conversions and tools. The app adds Windows-only entries and actions.</summary>
    public static OperationCatalog CreateDefault()
    {
        (string Label, string Extension, MagickFormat Format)[] imageTargets =
        [
            ("JPG", "jpg", MagickFormat.Jpeg),
            ("PNG", "png", MagickFormat.Png),
            ("WEBP", "webp", MagickFormat.WebP),
            ("HEIC", "heic", MagickFormat.Heic),
            ("AVIF", "avif", MagickFormat.Avif),
            ("GIF", "gif", MagickFormat.Gif),
            ("BMP", "bmp", MagickFormat.Bmp),
            ("TIFF", "tiff", MagickFormat.Tiff),
            ("ICO", "ico", MagickFormat.Icon),
            ("PDF", "pdf", MagickFormat.Pdf),
        ];

        return new OperationCatalog()
            .Add(WheelMode.Convert, imageTargets
                .Where(t => ImageConvertOperation.CanWrite(t.Format))
                .Select(t => new ImageConvertOperation(t.Label, t.Extension, t.Format)))
            .Add(WheelMode.Convert, MediaOperations.Conversions())
            .Add(WheelMode.Tools,
                new ImageCompressOperation(),
                new ImageResizeOperation(50),
                new ImageResizeOperation(25),
                new ImageRotateOperation(),
                new ImageStripMetadataOperation(),
                new ImagesToPdfOperation())
            .Add(WheelMode.Tools, MediaOperations.Tools())
            .Add(WheelMode.Tools,
                new PdfMergeOperation(),
                new PdfSplitOperation(),
                new PdfRotateOperation(),
                new ExtractOperation(),
                new ZipOperation());
    }
}
