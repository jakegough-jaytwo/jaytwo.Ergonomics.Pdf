using System;
using System.IO;
using jaytwo.Pdf;

namespace jaytwo.Ergonomics.Pdf.Internal;

// One run's open input: the document, the stream it reads, and the file the run owns, if any.
internal sealed class WorkflowSession : IDisposable
{
    private readonly FileStream? _owned;

    public WorkflowSession(PdfDocument document, Stream? stream, FileStream? owned, long inputLength)
    {
        Document = document;
        Stream = stream;
        _owned = owned;
        InputLength = inputLength;
    }

    public PdfDocument Document { get; }

    // The stream the document reads, or null for an IPdfSource input.
    public Stream? Stream { get; }

    public long InputLength { get; }

    public void Dispose()
    {
        Document.Dispose();
        _owned?.Dispose();
    }
}
