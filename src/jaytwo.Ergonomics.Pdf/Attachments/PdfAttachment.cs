using System;
using System.IO;
using jaytwo.Ergonomics.Pdf.Internal;

namespace jaytwo.Ergonomics.Pdf.Attachments;

/// <summary>
/// An embedded file in the document name tree.
/// </summary>
public sealed class PdfAttachment
{
    private readonly PdfEngine _engine;
    private readonly string? _creationDate;
    private readonly string? _modificationDate;

    internal PdfAttachment(PdfEngine engine, string name, string? description, string? mimeType, long? size, PdfAttachmentRelationship relationship, string? creationDate, string? modificationDate)
    {
        _engine = engine;
        Name = name;
        Description = description;
        MimeType = mimeType;
        Size = size;
        Relationship = relationship;
        _creationDate = creationDate;
        _modificationDate = modificationDate;
    }

    /// <summary>
    /// Gets the attachment name: the <c>/EmbeddedFiles</c> name-tree key when that key is a text string, otherwise
    /// the file specification's <c>/UF</c>, then <c>/F</c>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the description from /Desc.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets the MIME type from the embedded file /Subtype.
    /// </summary>
    public string? MimeType { get; }

    /// <summary>
    /// Gets /Params /Size, or <see langword="null"/> when the parameter is absent.
    /// </summary>
    public long? Size { get; }

    /// <summary>
    /// Gets the file specification's /AFRelationship. A value that is absent or not one of the
    /// <see cref="PdfAttachmentRelationship"/> names reads as <see cref="PdfAttachmentRelationship.Unspecified"/>.
    /// </summary>
    public PdfAttachmentRelationship Relationship { get; }

    /// <summary>
    /// Gets /Params /CreationDate, or <see langword="null"/> when the parameter is absent.
    /// </summary>
    /// <exception cref="PdfParseException">The date is not a PDF date.</exception>
    public DateTimeOffset? CreationDate => _creationDate == null ? null : PdfTextCodec.ParseDate(_creationDate);

    /// <summary>
    /// Gets /Params /ModDate, or <see langword="null"/> when the parameter is absent.
    /// </summary>
    /// <exception cref="PdfParseException">The date is not a PDF date.</exception>
    public DateTimeOffset? ModificationDate => _modificationDate == null ? null : PdfTextCodec.ParseDate(_modificationDate);

    /// <summary>
    /// Opens the decoded file bytes. The caller disposes the stream.
    /// </summary>
    /// <returns>A readable stream of the attachment.</returns>
    public Stream Open()
    {
        return _engine.OpenAttachment(Name);
    }
}
