using ImageMagick;

namespace Satsuma.Core.Images;

/// <summary>Converts images to another format, keeping animation where the target supports it.</summary>
public sealed class ImageConvertOperation(string label, string extension, MagickFormat format) : PerFileOperation
{
    static readonly HashSet<MagickFormat> MultiFrame = [MagickFormat.Gif, MagickFormat.WebP, MagickFormat.Tiff, MagickFormat.Pdf];
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
            using var frames = ImageFrames.Read(input, keepIconSizes: false);
            var animated = ImageFrames.IsAnimation(frames);
            // A PDF gets every page of a TIFF, but a GIF turned into a PDF is just its first frame.
            if (MultiFrame.Contains(format) && frames.Count > 1 && !(format == MagickFormat.Pdf && animated))
            {
                if (animated)
                    frames.Coalesce();
                foreach (var frame in frames)
                    Prepare(frame, context);
                ImageFrames.Write(frames, output, format, animated);
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
                    // Icons are square, so pad wide or tall pictures with transparency instead of stretching them.
                    if (image.Width != image.Height)
                    {
                        image.Alpha(AlphaAction.Set);
                        image.BackgroundColor = MagickColors.Transparent;
                        var side = Math.Max(image.Width, image.Height);
                        image.Extent(side, side, Gravity.Center);
                    }
                    if (image.Width > 256 || image.Height > 256)
                        image.Resize(256, 256);
                    image.Settings.SetDefine(MagickFormat.Icon, "auto-resize", "256,128,64,48,32,16");
                }
                ImageFrames.Write(image, output, format);
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

    /// <summary>Edits save in the original format, so camera RAW files and SVGs (read-only here) are left out.</summary>
    public override bool AppliesTo(IReadOnlyList<string> inputs) =>
        FileKinds.All(inputs, FileKind.Image) && inputs.All(i => CanSaveAs(Path.GetExtension(i)));

    static bool CanSaveAs(string extension)
    {
        var name = extension.TrimStart('.');
        return !name.Equals("svg", StringComparison.OrdinalIgnoreCase)
            && Enum.TryParse<MagickFormat>(name, ignoreCase: true, out var format)
            && ImageConvertOperation.CanWrite(format);
    }

    protected abstract void Edit(IMagickImage<byte> image, JobContext context);

    /// <summary>A chance to look at the saved file, e.g. to check it came out smaller.</summary>
    protected virtual void Saved(string input, string output) { }

    protected override Task<string> RunOneAsync(string input, JobContext context, IProgress<double>? progress) =>
        Task.Run(() =>
        {
            var output = context.Output.For(input, Path.GetExtension(input), Suffix);
            using var frames = ImageFrames.Read(input, keepIconSizes: true);
            // Only animations need full frames. TIFF pages and icon sizes would get cropped to the first one.
            var animated = ImageFrames.IsAnimation(frames);
            if (animated)
                frames.Coalesce();
            foreach (var frame in frames)
                Edit(frame, context);
            ImageFrames.Write(frames, output, null, animated);
            Saved(input, output);
            return output;
        }, context.Cancellation);
}

/// <summary>Reading and writing helpers for files that hold more than one picture.</summary>
static class ImageFrames
{
    static readonly HashSet<MagickFormat> AnimationFormats = [MagickFormat.Gif, MagickFormat.WebP, MagickFormat.APng, MagickFormat.Mng];
    static readonly HashSet<MagickFormat> IconFormats = [MagickFormat.Ico, MagickFormat.Icon, MagickFormat.Cur];

    /// <summary>
    /// Reads every frame, except a PSD keeps just its flattened picture (the rest are layers) and an icon
    /// keeps just its biggest size unless <paramref name="keepIconSizes"/> is set.
    /// </summary>
    public static MagickImageCollection Read(string input, bool keepIconSizes)
    {
        var frames = new MagickImageCollection(input);
        if (frames.Count < 2)
            return frames;

        var first = frames[0];
        var keep = first.Format switch
        {
            MagickFormat.Psd or MagickFormat.Psb => first,
            _ when IconFormats.Contains(first.Format) && !keepIconSizes => frames.MaxBy(f => (long)f.Width * f.Height)!,
            _ => null,
        };
        if (keep is null)
            return frames;

        var single = new MagickImageCollection { keep.Clone() };
        frames.Dispose();
        return single;
    }

    public static bool IsAnimation(MagickImageCollection frames) =>
        frames.Count > 1 && (AnimationFormats.Contains(frames[0].Format) || frames.Any(f => f.AnimationDelay > 0));

    /// <summary>Writes the frames as <paramref name="format"/>, or in the format they were read as when it's null.</summary>
    public static void Write(MagickImageCollection frames, string output, MagickFormat? format, bool animated)
    {
        format ??= frames[0].Format;
        // Coalescing turns every frame into a full picture, which makes GIFs much bigger. Shrink them back.
        if (animated && format == MagickFormat.Gif)
        {
            frames.Optimize();
            frames.OptimizeTransparency();
        }
        WriteFile(output, stream => frames.Write(stream, format.Value));
    }

    public static void Write(IMagickImage<byte> image, string output, MagickFormat format) =>
        WriteFile(output, stream => image.Write(stream, format));

    // Goes through a stream because ImageMagick treats "%" in file names as a placeholder, so
    // "photo (50%).gif" would end up somewhere else.
    static void WriteFile(string output, Action<Stream> write)
    {
        try
        {
            using var stream = File.Create(output);
            write(stream);
        }
        catch
        {
            File.Delete(output);
            throw;
        }
    }
}

public sealed class ImageCompressOperation : ImageEditOperation
{
    public override string Id => "image.compress";
    public override string Label => "Compress";
    public override string Glyph => "\uE73F";
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

    protected override void Saved(string input, string output)
    {
        // Some files (well-optimized GIFs, low-quality JPGs) can't get any smaller here. Hand back the original
        // rather than a bigger file.
        if (new FileInfo(output).Length > new FileInfo(input).Length)
            File.Copy(input, output, overwrite: true);
    }
}

public sealed class ImageResizeOperation(int percent) : ImageEditOperation
{
    public override string Id => $"image.resize.{percent}";
    public override string Label => $"{percent}%";
    public override string Glyph => "\uE740";
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
    public override string Glyph => "\uE7AD";
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
    public override string Glyph => "\uE72E";
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
    public override string Glyph => "\uEA90";
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
