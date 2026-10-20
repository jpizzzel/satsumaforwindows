using System.IO;
using ImageMagick;
using Satsuma.App.Interop;
using Satsuma.App.Jobs;
using Satsuma.App.Wheel;
using Satsuma.Core;
using Satsuma.Core.Media;

namespace Satsuma.App;

/// <summary>
/// Run "Satsuma.exe --self-test &lt;folder&gt;" to check the Windows-only bits on a real desktop (CI does this):
/// the mouse hook, the wheel and progress windows, PDF rendering, the tray icon and a few real conversions.
/// It writes self-test.log plus PNG previews of the wheels into the folder, and exits with 1 if anything broke.
/// </summary>
static class SelfTest
{
    public static async Task<int> RunAsync(string folder)
    {
        Directory.CreateDirectory(folder);
        var log = new List<string>();
        var failures = 0;

        async Task Check(string name, Func<Task<string>> step)
        {
            try
            {
                log.Add($"PASS {name}: {await step()}");
            }
            catch (Exception e)
            {
                failures++;
                log.Add($"FAIL {name}: {e}");
            }
        }

        var settings = new AppSettings();
        var hud = new HudWindow();
        var catalog = App.CreateCatalog(hud);
        string Sample(string name) => Path.Combine(folder, name);

        var png = Sample("sample.png");
        using (var image = new MagickImage(MagickColors.Orange, 320, 200))
            image.Write(png);
        var pdf = Sample("sample.pdf");
        using (var pages = new MagickImageCollection())
        {
            pages.Add(new MagickImage(MagickColors.White, 300, 400));
            pages.Add(new MagickImage(MagickColors.LightBlue, 300, 400));
            pages.Write(pdf, MagickFormat.Pdf);
        }

        JobContext Context(string input) => new() { Inputs = [input], Output = settings.Output(), Options = settings.Options() };
        async Task<string> Convert(WheelMode mode, string label, string input)
        {
            var operation = catalog.For(mode, [input]).Single(o => o.Label == label);
            var output = (await operation.RunAsync(Context(input))).Single();
            if (!File.Exists(output) && !Directory.Exists(output))
                throw new FileNotFoundException("No output", output);
            return Path.GetFileName(output);
        }

        await Check("catalog", () => Task.FromResult(string.Join("; ", Enum.GetValues<WheelMode>().Select(m =>
            $"{m}[png]={string.Join(",", catalog.For(m, [png]).Select(o => o.Label))}"))));
        await Check("png to jpg", () => Convert(WheelMode.Convert, "JPG", png));
        await Check("png to webp", () => Convert(WheelMode.Convert, "WEBP", png));
        await Check("heic writable", () => Task.FromResult(
            catalog.For(WheelMode.Convert, [png]).Any(o => o.Label == "HEIC") ? "yes" : "no (HEIC hidden from the wheel)"));
        await Check("pdf to png (Windows.Data.Pdf)", async () =>
        {
            var output = await Convert(WheelMode.Convert, "PNG", pdf);
            var pages = Directory.GetFiles(Sample(output));
            using var first = new MagickImage(pages.Order().First());
            return $"{pages.Length} pages, first is {first.Width}x{first.Height}";
        });
        await Check("pdf merge", async () =>
        {
            var merge = catalog.For(WheelMode.Tools, [pdf, pdf]).Single(o => o.Label == "Merge");
            return Path.GetFileName((await merge.RunAsync(new JobContext { Inputs = [pdf, pdf], Output = settings.Output() })).Single());
        });

        var ffmpeg = Ffmpeg.Locate();
        log.Add($"INFO ffmpeg: {ffmpeg ?? "not found"}");
        if (ffmpeg is not null)
        {
            var clip = Sample("clip.mp4");
            await Check("make sample video", async () =>
            {
                var start = new System.Diagnostics.ProcessStartInfo(ffmpeg) { CreateNoWindow = true };
                foreach (var arg in (string[])["-loglevel", "error", "-y", "-f", "lavfi", "-i", "testsrc=duration=2:size=320x240:rate=15",
                             "-f", "lavfi", "-i", "sine=duration=2", "-shortest", "-pix_fmt", "yuv420p", clip])
                    start.ArgumentList.Add(arg);
                using var process = System.Diagnostics.Process.Start(start)!;
                await process.WaitForExitAsync();
                return $"exit {process.ExitCode}";
            });
            await Check("mp4 to gif", () => Convert(WheelMode.Convert, "GIF", clip));
            await Check("mp4 to mp3", () => Convert(WheelMode.Convert, "MP3", clip));
            await Check("mp4 compress", () => Convert(WheelMode.Tools, "Compress", clip));
        }

        var wheel = new WheelWindow(catalog, settings);
        await Check("wheel previews", () =>
        {
            var shown = new List<string>();
            foreach (var (name, files, mode, hover) in new (string, string[], WheelMode, int)[]
                     {
                         ("wheel-convert-image.png", [png], WheelMode.Convert, 1),
                         ("wheel-convert-video.png", [Sample("clip.mp4")], WheelMode.Convert, 6),
                         ("wheel-tools-pdfs.png", [pdf, pdf], WheelMode.Tools, 0),
                         ("wheel-actions.png", [png], WheelMode.Actions, 3),
                     })
            {
                var operations = wheel.RenderPreview(files, mode, hover, Sample(name));
                shown.Add($"{name}: {string.Join(",", operations.Select(o => o.Label))}");
            }
            return Task.FromResult(string.Join(" | ", shown));
        });
        await Check("wheel shows under cursor", () =>
        {
            Native.GetCursorPos(out var cursor);
            wheel.Arm(WheelMode.Convert, cursor);
            var visible = wheel.IsVisible;
            Native.GetWindowRect(new System.Windows.Interop.WindowInteropHelper(wheel).Handle, out var rect);
            wheel.Disarm();
            var centeredX = Math.Abs((rect.Left + rect.Right) / 2 - cursor.X) <= 1;
            var centeredY = Math.Abs((rect.Top + rect.Bottom) / 2 - cursor.Y) <= 1;
            if (!visible || !centeredX || !centeredY)
                throw new InvalidOperationException($"visible={visible} rect={rect.Left},{rect.Top},{rect.Right},{rect.Bottom} cursor={cursor.X},{cursor.Y}");
            return Task.FromResult($"centered at {cursor.X},{cursor.Y}, {rect.Right - rect.Left}px wide");
        });
        await Check("mouse hook", () =>
        {
            using var watcher = new DragWatcher(System.Windows.Threading.Dispatcher.CurrentDispatcher, () => null);
            return Task.FromResult("installed and removed");
        });
        await Check("hud card", async () =>
        {
            var card = hud.Start("Converting to PNG", "sample.heic", null);
            card.Report(0.5);
            await Task.Delay(200);
            card.Succeed("Saved", "sample.png", [png]);
            card.Dismiss();
            return "shown and dismissed";
        });
        await Check("tray icon", () =>
        {
            using var tray = new TrayIcon(() => { }, _ => { }, () => { });
            return Task.FromResult("created");
        });

        log.Add(failures == 0 ? "ALL PASSED" : $"{failures} FAILED");
        await File.WriteAllLinesAsync(Path.Combine(folder, "self-test.log"), log);
        return failures == 0 ? 0 : 1;
    }
}
