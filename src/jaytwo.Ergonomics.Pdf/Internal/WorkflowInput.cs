using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Pdf;

namespace jaytwo.Ergonomics.Pdf.Internal;

// The input a workflow is bound to (spec F1, F2). A caller's stream or source is borrowed; a file is opened only for the run.
internal sealed class WorkflowInput
{
    private readonly Stream? _stream;
    private readonly IPdfSource? _source;
    private readonly string? _path;
    private readonly string? _password;

    private WorkflowInput(Stream? stream, IPdfSource? source, string? path, PdfWorkflowOpenOptions? options)
    {
        _stream = stream;
        _source = source;
        _path = path;
        _password = options?.Password;
    }

    public bool IsSource => _source != null;

    public bool IsWritableStream => _stream?.CanWrite ?? true;

    // The input's file path, when it has one: the path it was bound to, or a file stream's name.
    public string? FilePath => _path ?? (_stream as FileStream)?.Name;

    public static WorkflowInput Of(Stream stream, PdfWorkflowOpenOptions? options)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new ArgumentException("The input stream must be readable and seekable.", nameof(stream));
        }

        return new WorkflowInput(stream, null, null, options);
    }

    public static WorkflowInput Of(IPdfSource source, PdfWorkflowOpenOptions? options)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return new WorkflowInput(null, source, null, options);
    }

    public static WorkflowInput OfFile(string path, PdfWorkflowOpenOptions? options)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (path.Length == 0)
        {
            throw new ArgumentException("The input path is empty.", nameof(path));
        }

        return new WorkflowInput(null, null, Path.GetFullPath(path), options);
    }

    public bool IsStream(Stream stream)
    {
        return ReferenceEquals(_stream, stream);
    }

    public async Task<WorkflowSession> OpenAsync(bool writable, bool useAsync, CancellationToken cancellationToken)
    {
        FileStream? owned = null;
        try
        {
            Stream? stream = _stream;
            if (_path != null)
            {
                owned = new FileStream(_path, FileMode.Open, writable ? FileAccess.ReadWrite : FileAccess.Read, FileShare.Read, 4096, useAsync);
                stream = owned;
            }

            PdfDocument document;
            long length;
            if (stream != null)
            {
                length = stream.Length;
                document = useAsync
                    ? await PdfDocument.OpenAsync(stream, _password, leaveOpen: true, cancellationToken).ConfigureAwait(false)
                    : PdfDocument.Open(stream, _password, leaveOpen: true);
            }
            else
            {
                length = _source!.Length;
                document = useAsync
                    ? await PdfDocument.OpenAsync(_source, _password, leaveOpen: true, cancellationToken).ConfigureAwait(false)
                    : PdfDocument.Open(_source, _password, leaveOpen: true);
            }

            return new WorkflowSession(document, stream, owned, length);
        }
        catch
        {
            owned?.Dispose();
            throw;
        }
    }
}
