using System;
using System.Collections.Generic;

namespace jaytwo.Pdf;

/// <summary>One independent input occurrence in a new-file assembly.</summary>
public sealed class PdfAssemblyInput
{
    /// <summary>Initializes a new instance of the <see cref="PdfAssemblyInput"/> class without reading its document or enumerating its pages.</summary>
    /// <param name="document">An open document, borrowed until assembly completes.</param>
    /// <param name="pageNumbers">Positive, 1-based page numbers in output order, with repetitions allowed.</param>
    public PdfAssemblyInput(PdfDocument document, IEnumerable<int> pageNumbers)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        PageNumbers = pageNumbers ?? throw new ArgumentNullException(nameof(pageNumbers));
    }

    /// <summary>Gets the borrowed input document.</summary>
    public PdfDocument Document { get; }

    /// <summary>Gets the ordered selection, enumerated once during assembly.</summary>
    public IEnumerable<int> PageNumbers { get; }
}
