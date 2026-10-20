using System.IO;
using Satsuma.Core;

namespace Satsuma.App.Jobs;

/// <summary>Runs a chosen operation in the background and reports it on the HUD.</summary>
sealed class JobRunner(AppSettings settings, HudWindow hud)
{
    public async void Run(FileOperation operation, IReadOnlyList<string> files)
    {
        var subject = files.Count == 1 ? Path.GetFileName(files[0].TrimEnd('\\')) : $"{files.Count} items";
        var card = hud.Start(operation.Description, subject, operation.Glyph);
        using var cancel = new CancellationTokenSource();
        card.AttachCancellation(cancel);

        var context = new JobContext
        {
            Inputs = files,
            Output = settings.Output(),
            Options = settings.Options(),
            Progress = new Progress<double>(card.Report), // Created on the UI thread, so reports land there.
            Cancellation = cancel.Token,
        };

        try
        {
            var outputs = operation.RunsOnUiThread
                ? await operation.RunAsync(context)
                : await Task.Run(() => operation.RunAsync(context), cancel.Token);

            var detail = outputs switch
            {
                [] => subject,
                [var one] => Path.GetFileName(one),
                _ => $"{outputs.Count} files",
            };
            card.Succeed(outputs.Count == 0 ? $"{operation.Label}: done" : "Saved", detail, outputs);
        }
        catch (OperationCanceledException)
        {
            card.Fail("Cancelled", subject);
        }
        catch (Exception e)
        {
            Log.Write($"{operation.Id} failed for {string.Join(", ", files)}: {e}");
            card.Fail($"{operation.Description} failed", e.Message);
        }
    }
}
