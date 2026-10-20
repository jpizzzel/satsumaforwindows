namespace Satsuma.Core.Tests;

public sealed class CatalogTests : IDisposable
{
    readonly TestFiles _files = new();
    readonly OperationCatalog _catalog = OperationCatalog.CreateDefault();

    public void Dispose() => _files.Dispose();

    IEnumerable<string> Labels(WheelMode mode, params string[] paths) => _catalog.For(mode, paths).Select(o => o.Label);

    [Theory]
    [InlineData("a.JPG", FileKind.Image)]
    [InlineData("a.heic", FileKind.Image)]
    [InlineData("a.mov", FileKind.Video)]
    [InlineData("a.flac", FileKind.Audio)]
    [InlineData("a.pdf", FileKind.Pdf)]
    [InlineData("a.srt", FileKind.Subtitle)]
    [InlineData("a.tar.gz", FileKind.Archive)]
    [InlineData("a.docx", FileKind.Unknown)]
    public void Classifies_by_extension(string name, FileKind expected) =>
        Assert.Equal(expected, FileKinds.Of(name));

    [Fact]
    public void Folders_are_folders()
    {
        Assert.Equal(FileKind.Folder, FileKinds.Of(_files.Root));
    }

    [Fact]
    public void Image_wheel_offers_other_formats_but_not_its_own()
    {
        var labels = Labels(WheelMode.Convert, "photo.jpeg").ToList();
        Assert.Contains("PNG", labels);
        Assert.Contains("WEBP", labels);
        Assert.Contains("PDF", labels);
        Assert.DoesNotContain("JPG", labels);
        Assert.DoesNotContain("MP3", labels);
    }

    [Fact]
    public void Video_wheel_offers_video_gif_and_audio()
    {
        var labels = Labels(WheelMode.Convert, "clip.mp4").ToList();
        Assert.Contains("MOV", labels);
        Assert.Contains("GIF", labels);
        Assert.Contains("MP3", labels);
        Assert.DoesNotContain("MP4", labels);
        Assert.DoesNotContain("PNG", labels);
    }

    [Fact]
    public void Audio_wheel_offers_no_video_formats()
    {
        var labels = Labels(WheelMode.Convert, "song.wav").ToList();
        Assert.Contains("MP3", labels);
        Assert.DoesNotContain("WAV", labels);
        Assert.DoesNotContain("MP4", labels);
    }

    [Fact]
    public void Mixed_kinds_share_only_common_entries()
    {
        Assert.Empty(Labels(WheelMode.Convert, "photo.png", "song.mp3"));
        Assert.Equal(["Zip"], Labels(WheelMode.Tools, "photo.png", "song.mp3"));
    }

    [Fact]
    public void Several_images_can_be_combined_but_one_cannot()
    {
        Assert.Contains("Combine PDF", Labels(WheelMode.Tools, "a.png", "b.jpg"));
        Assert.DoesNotContain("Combine PDF", Labels(WheelMode.Tools, "a.png"));
    }

    [Fact]
    public void Pdf_tools_include_merge_only_for_several()
    {
        Assert.Contains("Merge", Labels(WheelMode.Tools, "a.pdf", "b.pdf"));
        Assert.DoesNotContain("Merge", Labels(WheelMode.Tools, "a.pdf"));
        Assert.Contains("Split pages", Labels(WheelMode.Tools, "a.pdf"));
    }

    [Fact]
    public void Read_only_image_formats_get_conversions_but_no_edits()
    {
        Assert.Contains("JPG", Labels(WheelMode.Convert, "raw.dng"));
        Assert.DoesNotContain("Compress", Labels(WheelMode.Tools, "raw.dng"));
        Assert.DoesNotContain("Rotate", Labels(WheelMode.Tools, "logo.svg"));
        Assert.Contains("Compress", Labels(WheelMode.Tools, "photo.JPEG"));
        Assert.Contains("Compress", Labels(WheelMode.Tools, "photo.tif"));
    }

    [Fact]
    public void Core_catalog_has_no_actions()
    {
        Assert.Empty(Labels(WheelMode.Actions, "a.pdf"));
    }

    [Fact]
    public void Output_names_are_unique_and_sit_beside_the_input()
    {
        var input = _files.PathOf("photo.heic");
        var locator = new OutputLocator();

        Assert.Equal(_files.PathOf("photo.png"), locator.For(input, "png"));
        File.WriteAllText(_files.PathOf("photo.png"), "");
        Assert.Equal(_files.PathOf("photo (1).png"), locator.For(input, ".png"));
        Assert.Equal(_files.PathOf("photo (compressed).heic"), locator.For(input, "heic", "compressed"));
    }

    [Fact]
    public void Same_name_keeps_dotted_folder_names_whole()
    {
        var folder = Directory.CreateDirectory(_files.PathOf("v1.2 release")).FullName;
        var target = Path.Combine(_files.Root, "dest");
        var locator = new OutputLocator { Folder = target };

        Assert.Equal(Path.Combine(target, "v1.2 release"), locator.SameName(folder));
        Directory.CreateDirectory(Path.Combine(target, "v1.2 release"));
        Assert.Equal(Path.Combine(target, "v1.2 release (1)"), locator.SameName(folder));
        Assert.Equal(Path.Combine(target, "notes.txt"), locator.SameName(_files.PathOf("notes.txt")));
    }

    [Fact]
    public void Output_folder_setting_redirects_results()
    {
        var target = Path.Combine(_files.Root, "out");
        var locator = new OutputLocator { Folder = target };

        Assert.Equal(Path.Combine(target, "photo.png"), locator.For(_files.PathOf("photo.heic"), "png"));
        Assert.True(Directory.Exists(target));
    }

    [Fact]
    public void Dotted_folders_and_dotfiles_keep_their_whole_name()
    {
        var folder = Directory.CreateDirectory(_files.PathOf("release.v1.2")).FullName;
        var dotfile = _files.PathOf(".env");
        File.WriteAllText(dotfile, "");
        var locator = new OutputLocator();

        Assert.Equal(_files.PathOf("release.v1.2.zip"), locator.For(folder, "zip"));
        Assert.Equal(_files.PathOf(".env.zip"), locator.For(dotfile, "zip"));
    }

    string TransportStream(string name, int packetSize)
    {
        var bytes = new byte[packetSize * 2];
        var sync = packetSize == 192 ? 4 : 0;
        bytes[sync] = bytes[packetSize + sync] = 0x47;
        File.WriteAllBytes(_files.PathOf(name), bytes);
        return _files.PathOf(name);
    }

    [Fact]
    public void Ts_files_are_video_only_when_they_look_like_video()
    {
        Assert.Equal(FileKind.Video, FileKinds.Of(TransportStream("clip.ts", 188)));
        Assert.Equal(FileKind.Video, FileKinds.Of(TransportStream("clip.m2ts", 192)));
        Assert.Equal(FileKind.Video, FileKinds.Of(TransportStream("camera.MTS", 192)));

        var code = _files.PathOf("app.ts");
        File.WriteAllText(code, "export const greeting = 'Good morning';\n" + new string(' ', 300));
        Assert.Equal(FileKind.Unknown, FileKinds.Of(code));
        Assert.Equal(FileKind.Unknown, FileKinds.Of(_files.PathOf("missing.ts")));
        Assert.DoesNotContain("MP4", Labels(WheelMode.Convert, code));
    }

    [Fact]
    public async Task Per_file_progress_arrives_in_order_straight_away()
    {
        var reports = new List<double>();
        var context = new JobContext
        {
            Inputs = ["a", "b"],
            Output = new OutputLocator(),
            Progress = new SynchronousProgress(reports.Add),
            Cancellation = TestContext.Current.CancellationToken,
        };

        await new SteppingOperation().RunAsync(context);

        Assert.Equal([0.125, 0.25, 0.375, 0.5, 0.625, 0.75, 0.875, 1.0], reports);
    }

    sealed class SteppingOperation : PerFileOperation
    {
        public override string Id => "test.steps";
        public override string Label => "Steps";
        public override bool AppliesTo(IReadOnlyList<string> inputs) => true;

        protected override Task<string> RunOneAsync(string input, JobContext context, IProgress<double>? progress) =>
            Task.Run(() =>
            {
                foreach (var step in (double[])[0.25, 0.5, 0.75])
                    progress?.Report(step);
                return input;
            });
    }

    sealed class SynchronousProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
