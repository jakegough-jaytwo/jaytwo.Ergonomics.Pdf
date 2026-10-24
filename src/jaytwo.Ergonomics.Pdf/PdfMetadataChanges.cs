using jaytwo.Pdf.Metadata;

namespace jaytwo.Ergonomics.Pdf;

/// <summary>
/// Document information changes for <see cref="PdfWorkflowBuilder.WithMetadata"/> (spec F7).
/// </summary>
/// <remarks>
/// Only the properties an initializer assigns change. A property assigned <see langword="null"/> is removed from the
/// document; one left out keeps its current value. The engine refreshes the modification date and keeps matching XMP
/// properties in step.
/// </remarks>
public sealed class PdfMetadataChanges
{
    private readonly string? _title;
    private readonly string? _author;
    private readonly string? _subject;
    private readonly string? _keywords;
    private readonly string? _creator;
    private readonly string? _producer;
    private readonly bool _hasTitle;
    private readonly bool _hasAuthor;
    private readonly bool _hasSubject;
    private readonly bool _hasKeywords;
    private readonly bool _hasCreator;
    private readonly bool _hasProducer;

    /// <summary>
    /// Gets the title.
    /// </summary>
    public string? Title
    {
        get => _title;
        init
        {
            _title = value;
            _hasTitle = true;
        }
    }

    /// <summary>
    /// Gets the author.
    /// </summary>
    public string? Author
    {
        get => _author;
        init
        {
            _author = value;
            _hasAuthor = true;
        }
    }

    /// <summary>
    /// Gets the subject.
    /// </summary>
    public string? Subject
    {
        get => _subject;
        init
        {
            _subject = value;
            _hasSubject = true;
        }
    }

    /// <summary>
    /// Gets the keywords.
    /// </summary>
    public string? Keywords
    {
        get => _keywords;
        init
        {
            _keywords = value;
            _hasKeywords = true;
        }
    }

    /// <summary>
    /// Gets the creator, the application that made the original document.
    /// </summary>
    public string? Creator
    {
        get => _creator;
        init
        {
            _creator = value;
            _hasCreator = true;
        }
    }

    /// <summary>
    /// Gets the producer, the application that converted the document to PDF.
    /// </summary>
    public string? Producer
    {
        get => _producer;
        init
        {
            _producer = value;
            _hasProducer = true;
        }
    }

    internal void ApplyTo(PdfMetadataEditor metadata)
    {
        if (_hasTitle)
        {
            metadata.Title = _title;
        }

        if (_hasAuthor)
        {
            metadata.Author = _author;
        }

        if (_hasSubject)
        {
            metadata.Subject = _subject;
        }

        if (_hasKeywords)
        {
            metadata.Keywords = _keywords;
        }

        if (_hasCreator)
        {
            metadata.Creator = _creator;
        }

        if (_hasProducer)
        {
            metadata.Producer = _producer;
        }
    }
}
