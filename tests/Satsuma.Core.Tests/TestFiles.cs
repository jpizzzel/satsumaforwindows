using System.Diagnostics;
using ImageMagick;
using Satsuma.Core.Media;

namespace Satsuma.Core.Tests;

/// <summary>A scratch folder that is deleted after each test, with helpers to make sample files.</summary>
public sealed class TestFiles : IDisposable
{
    public string Root { get; } = Directory.CreateTempSubdirectory("satsuma-tests-").FullName;

    public string PathOf(string name) => Path.Combine(Root, name);

    public JobContext Context(params string[] inputs) => new()
    {
        Inputs = inputs,
        Output = new OutputLocator(),
        Cancellation = TestContext.Current.CancellationToken,
    };

    public string Image(string name, uint width = 64, uint height = 32, bool transparent = false)
    {
        var path = PathOf(name);
        using var image = new MagickImage(transparent ? MagickColors.Transparent : MagickColors.Orange, width, height);
        image.Draw(new ImageMagick.Drawing.Drawables().FillColor(MagickColors.Green).Rectangle(0, 0, width / 2.0, height / 2.0));
        image.Write(path);
        return path;
    }

    public string AnimatedGif(string name, int frames = 3)
    {
        var path = PathOf(name);
        using var collection = new MagickImageCollection();
        for (var i = 0; i < frames; i++)
        {
            var frame = new MagickImage(i % 2 == 0 ? MagickColors.Red : MagickColors.Blue, 20, 20) { AnimationDelay = 10 };
            collection.Add(frame);
        }
        collection.Write(path);
        return path;
    }

    public string Pdf(string name, int pages)
    {
        var path = PathOf(name);
        using var collection = new MagickImageCollection();
        for (var i = 0; i < pages; i++)
            collection.Add(new MagickImage(MagickColors.White, 100, 140));
        collection.Write(path, MagickFormat.Pdf);
        return path;
    }

    /// <summary>Builds a sample with ffmpeg's test sources, e.g. a tone or colour bars.</summary>
    public string Media(string name, params string[] arguments)
    {
        var ffmpeg = Ffmpeg.Locate()!;
        var start = new ProcessStartInfo(ffmpeg) { RedirectStandardError = true };
        foreach (var arg in (string[])["-hide_banner", "-loglevel", "error", "-y", .. arguments, PathOf(name)])
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var errors = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, errors);
        return PathOf(name);
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); }
        catch (IOException) { }
    }
}
