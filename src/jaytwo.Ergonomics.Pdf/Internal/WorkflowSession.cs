using System;
using System.Collections.Generic;
using System.IO;
using jaytwo.Pdf;

namespace jaytwo.Ergonomics.Pdf.Internal;

// One run's open input: the document, the stream it reads, and the files the run owns, if any.
internal sealed class WorkflowSession : IDisposable
{
    private readonly FileStream? _owned;
    private readonly List<FileStream> _attachments = new List<FileStream>();

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

    // Opens an attachment file for the rest of the run, so the engine's add-time and save-time reads see one file.
    public FileStream OpenAttachment(string path)
    {
        var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        _attachments.Add(file);
        return file;
    }

    public void Dispose()
    {
        Document.Dispose();
        foreach (FileStream file in _attachments)
        {
            file.Dispose();
        }

        _owned?.Dispose();
    }
}
