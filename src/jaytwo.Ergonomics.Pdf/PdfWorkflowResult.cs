using System.Collections.Generic;
using jaytwo.Pdf.AcroForm;

namespace jaytwo.Ergonomics.Pdf;

/// <summary>
/// The outcome of a workflow run (spec F13).
/// </summary>
public sealed class PdfWorkflowResult
{
    internal PdfWorkflowResult(bool modified, long outputLength, IReadOnlyDictionary<string, PdfAppearanceDisposition> fieldAppearances)
    {
        Modified = modified;
        OutputLength = outputLength;
        FieldAppearances = fieldAppearances;
    }

    /// <summary>
    /// Gets a value indicating whether the run appended a revision.
    /// </summary>
    public bool Modified { get; }

    /// <summary>
    /// Gets the length in bytes of the output: the destination, or the input after <see cref="PdfWorkflowBuilder.AppendToSource"/>.
    /// </summary>
    public long OutputLength { get; }

    /// <summary>
    /// Gets the appearance disposition of each filled field's final value, by full field name. It is empty when the
    /// workflow fills nothing.
    /// </summary>
    public IReadOnlyDictionary<string, PdfAppearanceDisposition> FieldAppearances { get; }

    internal static IReadOnlyDictionary<string, PdfAppearanceDisposition> NoFields { get; } = new Dictionary<string, PdfAppearanceDisposition>();
}
