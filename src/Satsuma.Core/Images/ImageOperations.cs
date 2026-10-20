using ImageMagick;

namespace Satsuma.Core.Images;

/// <summary>Converts images to another format, keeping animation where the target supports it.</summary>
public sealed class ImageConvertOperation(string label, string extension, MagickFormat format) : PerFileOperation
{
    static readonly HashSet<MagickFormat> MultiFrame = [MagickFormat.Gif, MagickFormat.WebP, MagickFormat.Tiff];
    static readonly HashSet<MagickFormat> NoAlpha = [MagickFormat.Jpeg, MagickFormat.Bmp];

    public override string Id => "image.convert." + extension;
    public override string Label => label;
    public override string Description => $"Converting to {label}";

    public static bool CanWrite(MagickFormat format) =>
        MagickNET.SupportedFormats.Any(f => f.Format == format && f.SupportsWriting);

    public override bool AppliesTo(IReadOnlyList<string> inputs) =>
        FileKinds.All(inputs, FileKind.Image)
        && !inputs.All(i => FileKinds.SameFormat(Path.GetExtension(i), extension));

    protected override Task<string> RunOneAsync(string input, JobContext context, IProgress<double>? progress) =>
        Task.Run(() =>
        {
            var output = context.Output.For(input, extension);
            using var frames = new MagickImageCollection(input);
            if (MultiFrame.Contains(format) && frames.Count > 1)
            {
                frames.Coalesce();
                foreach (var frame in frames)
                    Prepare(frame, context);
                frames.Write(output, format);
            }
            else
            {
                var image = frames[0];
                Prepare(image, context);
                if (NoAlpha.Contains(format) && image.HasAlpha)
                {
                    image.BackgroundColor = MagickColors.White;
                    image.Alpha(AlphaAction.Remove);
                }
                if (format == MagickFormat.Icon)
                {
                    if (image.Width > 256 || image.Height > 256)
                        image.Resize(256, 256);
                    image.Settings.SetDefine(MagickFormat.Icon, "auto-resize", "256,128,64,48,32,16");
                }
                image.Write(output, format);
            }
            return output;
        }, context.Cancellation);

    static void Prepare(IMagickImage<byte> image, JobContext context)
    {
        image.AutoOrient();
        image.Quality = (uint)Math.Clamp(context.Options.ImageQuality, 1, 100);
    }
}

/// <summary>Shared plumbing for tools that edit an image and save a copy in the same format.</summary>
public abstract class ImageEditOperation : PerFileOperation
{
    protected abstract string Suffix { get; }

    public override bool AppliesTo(IReadOnlyList<string> inputs) =>
        FileKinds.All(inputs, FileKind.Image) && inputs.All(i => !FileKinds.SameFormat(Path.GetExtension(i), ".svg"));

    protected abstract void Edit(IMagickImage<byte> image, JobContext context);

    protected override Task<string> RunOneAsync(string input, JobContext context, IProgress<double>? progress) =>
        Task.Run(() =>
        {
            var output = context.Output.For(input, Path.GetExtension(input), Suffix);
            using var frames = new MagickImageCollection(input);
            if (frames.Count > 1)
                frames.Coalesce();
            foreach (var frame in frames)
                Edit(frame, context);
            frames.Write(output);
            return output;
        }, context.Cancellation);
}

public sealed class ImageCompressOperation : ImageEditOperation
{
    public override string Id => "image.compress";
    public override string Label => "Compress";
    public override string Glyph => "";
    public override string Description => "Compressing";
    protected override string Suffix => "compressed";

    protected override void Edit(IMagickImage<byte> image, JobContext context)
    {
        image.AutoOrient();
        image.Strip();
        switch (image.Format)
        {
            case MagickFormat.Png or MagickFormat.Png8 or MagickFormat.Png24 or MagickFormat.Png32 or MagickFormat.Gif
                or MagickFormat.Bmp:
                // Lossless formats shrink the most by reducing the palette, like pngquant does.
                image.Quantize(new QuantizeSettings { Colors = 256, DitherMethod = DitherMethod.FloydSteinberg });
                image.Settings.SetDefine(MagickFormat.Png, "compression-level", "9");
                break;
            default:
                image.Quality = 70;
                break;
        }
    }
}

public sealed class ImageResizeOperation(int percent) : ImageEditOperation
{
    public override string Id => $"image.resize.{percent}";
    public override string Label => $"{percent}%";
    public override string Glyph => "";
    public override string Description => $"Resizing to {percent}%";
    protected override string Suffix => $"{percent}%";

    protected override void Edit(IMagickImage<byte> image, JobContext context)
    {
        image.AutoOrient();
        image.Resize(new Percentage(percent));
    }
}

public sealed class ImageRotateOperation : ImageEditOperation
{
    public override string Id => "image.rotate";
    public override string Label => "Rotate";
    public override string Glyph => "";
    public override string Description => "Rotating";
    protected override string Suffix => "rotated";

    protected override void Edit(IMagickImage<byte> image, JobContext context)
    {
        image.AutoOrient();
        image.Rotate(90);
    }
}

public sealed class ImageStripMetadataOperation : ImageEditOperation
{
    public override string Id => "image.strip";
    public override string Label => "No metadata";
    public override string Glyph => "";
    public override string Description => "Removing metadata";
    protected override string Suffix => "no metadata";

    protected override void Edit(IMagickImage<byte> image, JobContext context)
    {
        // Bake the orientation into the pixels first so the photo doesn't flip once EXIF is gone.
        image.AutoOrient();
        image.Strip();
    }
}

/// <summary>Puts several images into one PDF, one page each, in the order they were dragged.</summary>
public sealed class ImagesToPdfOperation : FileOperation
{
    public override string Id => "image.combine-pdf";
    public override string Label => "Combine PDF";
    public override string Glyph => "";
    public override string Description => "Combining into PDF";

    public override bool AppliesTo(IReadOnlyList<string> inputs) =>
        inputs.Count >= 2 && FileKinds.All(inputs, FileKind.Image);

    public override Task<IReadOnlyList<string>> RunAsync(JobContext context) =>
        Task.Run<IReadOnlyList<string>>(() =>
        {
            using var pages = new MagickImageCollection();
            for (var i = 0; i < context.Inputs.Count; i++)
            {
                context.Cancellation.ThrowIfCancellationRequested();
                var page = new MagickImage(context.Inputs[i]);
                page.AutoOrient();
                if (page.HasAlpha)
                {
                    page.BackgroundColor = MagickColors.White;
                    page.Alpha(AlphaAction.Remove);
                }
                page.Quality = (uint)Math.Clamp(context.Options.ImageQuality, 1, 100);
                pages.Add(page);
                context.Progress?.Report((i + 1.0) / (context.Inputs.Count + 1));
            }
            var output = context.Output.Named(context.Inputs[0],
                Path.GetFileNameWithoutExtension(context.Inputs[0]) + " (combined).pdf");
            pages.Write(output, MagickFormat.Pdf);
            return [output];
        }, context.Cancellation);
}
