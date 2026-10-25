using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using jaytwo.Ergonomics.Pdf.Internal;
using jaytwo.Pdf;

namespace jaytwo.Ergonomics.Pdf;

/// <summary>One independent occurrence in a combine workflow, with an ordered page selection.</summary>
public sealed class PdfCombineInput
{
    private PdfCombineInput(WorkflowInput input, IEnumerable<int>? pageNumbers)
    {
        Input = input;
        PageNumbers = pageNumbers?.ToArray();
        if (PageNumbers != null && (PageNumbers.Length == 0 || PageNumbers.Any(page => page < 1)))
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumbers), "Select at least one positive, 1-based page number.");
        }
    }

    internal WorkflowInput Input { get; }

    internal int[]? PageNumbers { get; }

    /// <summary>Describes a borrowed readable seekable stream without reading it.</summary>
    /// <param name="input">The PDF, kept unchanged until the run ends; never disposed by the workflow.</param>
    /// <param name="pageNumbers">Ordered positive, 1-based pages, copied now; null selects all pages. Use a sequence such as Enumerable.Range for a range.</param>
    /// <param name="options">The password, copied now.</param>
    /// <returns>The input occurrence.</returns>
    public static PdfCombineInput From(Stream input, IEnumerable<int>? pageNumbers = null, PdfWorkflowOpenOptions? options = null)
    {
        return new PdfCombineInput(WorkflowInput.Of(input, options), pageNumbers);
    }

    /// <summary>Describes a borrowed random-access snapshot without reading it.</summary>
    /// <param name="input">The snapshot, kept unchanged until the run ends; never disposed by the workflow.</param>
    /// <param name="pageNumbers">Ordered positive, 1-based pages, copied now; null selects all pages.</param>
    /// <param name="options">The password, copied now.</param>
    /// <returns>The input occurrence.</returns>
    public static PdfCombineInput From(IPdfSource input, IEnumerable<int>? pageNumbers = null, PdfWorkflowOpenOptions? options = null)
    {
        return new PdfCombineInput(WorkflowInput.Of(input, options), pageNumbers);
    }

    /// <summary>Describes a file opened for reading only during the run and closed afterward, including on failure.</summary>
    /// <param name="path">The PDF path, resolved now.</param>
    /// <param name="pageNumbers">Ordered positive, 1-based pages, copied now; null selects all pages.</param>
    /// <param name="options">The password, copied now.</param>
    /// <returns>The input occurrence.</returns>
    public static PdfCombineInput FromFile(string path, IEnumerable<int>? pageNumbers = null, PdfWorkflowOpenOptions? options = null)
    {
        return new PdfCombineInput(WorkflowInput.OfFile(path, options), pageNumbers);
    }
}
