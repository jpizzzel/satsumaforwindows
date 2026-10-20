using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Satsuma.Core.Documents;

public sealed class PdfMergeOperation : FileOperation
{
    public override string Id => "pdf.merge";
    public override string Label => "Merge";
    public override string Glyph => "\uE8F1";
    public override string Description => "Merging PDFs";

    public override bool AppliesTo(IReadOnlyList<string> inputs) =>
        inputs.Count >= 2 && FileKinds.All(inputs, FileKind.Pdf);

    public override Task<IReadOnlyList<string>> RunAsync(JobContext context) =>
        Task.Run<IReadOnlyList<string>>(() =>
        {
            using var merged = new PdfDocument();
            for (var i = 0; i < context.Inputs.Count; i++)
            {
                context.Cancellation.ThrowIfCancellationRequested();
                using var source = PdfReader.Open(context.Inputs[i], PdfDocumentOpenMode.Import);
                foreach (var page in source.Pages)
                    merged.AddPage(page);
                context.Progress?.Report((i + 1.0) / (context.Inputs.Count + 1));
            }
            var output = context.Output.Named(context.Inputs[0],
                Path.GetFileNameWithoutExtension(context.Inputs[0]) + " (merged).pdf");
            merged.Save(output);
            return [output];
        }, context.Cancellation);
}

/// <summary>Writes each page to its own PDF inside a new folder.</summary>
public sealed class PdfSplitOperation : PerFileOperation
{
    public override string Id => "pdf.split";
    public override string Label => "Split pages";
    public override string Glyph => "\uE8C6";
    public override string Description => "Splitting into pages";

    public override bool AppliesTo(IReadOnlyList<string> inputs) => FileKinds.All(inputs, FileKind.Pdf);

    protected override Task<string> RunOneAsync(string input, JobContext context, IProgress<double>? progress) =>
        Task.Run(() =>
        {
            using var source = PdfReader.Open(input, PdfDocumentOpenMode.Import);
            var folder = context.Output.FolderFor(input, "pages");
            Directory.CreateDirectory(folder);
            var name = Path.GetFileNameWithoutExtension(input);
            var digits = source.PageCount.ToString().Length;
            for (var i = 0; i < source.PageCount; i++)
            {
                context.Cancellation.ThrowIfCancellationRequested();
                using var single = new PdfDocument();
                single.AddPage(source.Pages[i]);
                single.Save(Path.Combine(folder, $"{name} - page {(i + 1).ToString().PadLeft(digits, '0')}.pdf"));
                progress?.Report((i + 1.0) / source.PageCount);
            }
            return folder;
        }, context.Cancellation);
}

public sealed class PdfRotateOperation : PerFileOperation
{
    public override string Id => "pdf.rotate";
    public override string Label => "Rotate";
    public override string Glyph => "\uE7AD";
    public override string Description => "Rotating pages";

    public override bool AppliesTo(IReadOnlyList<string> inputs) => FileKinds.All(inputs, FileKind.Pdf);

    protected override Task<string> RunOneAsync(string input, JobContext context, IProgress<double>? progress) =>
        Task.Run(() =>
        {
            using var document = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
            foreach (var page in document.Pages)
                page.Rotate = (page.Rotate + 90) % 360;
            var output = context.Output.For(input, "pdf", "rotated");
            document.Save(output);
            return output;
        }, context.Cancellation);
}
