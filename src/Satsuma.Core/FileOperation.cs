namespace Satsuma.Core;

public sealed class ConversionOptions
{
    /// <summary>Quality (1-100) used for lossy image output such as JPG, WEBP and AVIF.</summary>
    public int ImageQuality { get; set; } = 90;
}

public sealed class JobContext
{
    public required IReadOnlyList<string> Inputs { get; init; }
    public required OutputLocator Output { get; init; }
    public ConversionOptions Options { get; init; } = new();
    public IProgress<double>? Progress { get; init; }
    public CancellationToken Cancellation { get; init; }
}

/// <summary>One entry on the wheel: a conversion, a tool or an action.</summary>
public abstract class FileOperation
{
    public abstract string Id { get; }

    /// <summary>Short text shown on the wheel segment, e.g. "PNG" or "Compress".</summary>
    public abstract string Label { get; }

    /// <summary>Optional Segoe Fluent Icons glyph shown above the label.</summary>
    public virtual string? Glyph => null;

    /// <summary>Verb used in progress messages, e.g. "Converting to PNG".</summary>
    public virtual string Description => Label;

    /// <summary>Operations that touch UI state (clipboard, dialogs) must run on the UI thread.</summary>
    public virtual bool RunsOnUiThread => false;

    public abstract bool AppliesTo(IReadOnlyList<string> inputs);

    /// <summary>Runs the operation and returns the paths it created (may be empty for actions).</summary>
    public abstract Task<IReadOnlyList<string>> RunAsync(JobContext context);
}

/// <summary>An operation that processes each input on its own and reports combined progress.</summary>
public abstract class PerFileOperation : FileOperation
{
    public override async Task<IReadOnlyList<string>> RunAsync(JobContext context)
    {
        var outputs = new List<string>();
        var count = context.Inputs.Count;
        for (var i = 0; i < count; i++)
        {
            context.Cancellation.ThrowIfCancellationRequested();
            var index = i;
            // Forward straight away so reports can't arrive out of order and move the bar backwards.
            var fileProgress = context.Progress is null
                ? null
                : new ForwardProgress(p => context.Progress.Report((index + Math.Clamp(p, 0, 1)) / count));
            outputs.Add(await RunOneAsync(context.Inputs[i], context, fileProgress));
            context.Progress?.Report((double)(i + 1) / count);
        }
        return outputs;
    }

    protected abstract Task<string> RunOneAsync(string input, JobContext context, IProgress<double>? progress);

    sealed class ForwardProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
