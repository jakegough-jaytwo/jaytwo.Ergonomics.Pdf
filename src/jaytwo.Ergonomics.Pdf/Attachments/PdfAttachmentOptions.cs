using System;

namespace jaytwo.Ergonomics.Pdf.Attachments;

/// <summary>
/// Optional information stored with a new embedded file.
/// </summary>
public sealed class PdfAttachmentOptions
{
    /// <summary>
    /// Gets the file description stored in /Desc.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the MIME type stored as the embedded file /Subtype.
    /// </summary>
    public string? MimeType { get; init; }

    /// <summary>
    /// Gets the relationship stored as the file specification's /AFRelationship. The default is
    /// <see cref="PdfAttachmentRelationship.Unspecified"/>.
    /// </summary>
    public PdfAttachmentRelationship Relationship { get; init; }

    /// <summary>
    /// Gets the file creation date stored in /Params /CreationDate, to the second. When it is
    /// <see langword="null"/>, the time of <see cref="PdfAttachmentCollection.Add"/> is stored.
    /// </summary>
    public DateTimeOffset? CreationDate { get; init; }

    /// <summary>
    /// Gets the file modification date stored in /Params /ModDate, to the second. When it is
    /// <see langword="null"/>, the time of <see cref="PdfAttachmentCollection.Add"/> is stored.
    /// </summary>
    public DateTimeOffset? ModificationDate { get; init; }

    /// <summary>
    /// Gets a value indicating whether the content is written Flate-encoded. The content is compressed while it is
    /// streamed, in constant memory, once to measure it during <see cref="PdfAttachmentCollection.Add"/> and again
    /// when the document is saved. The default is <see langword="false"/>.
    /// </summary>
    public bool Compress { get; init; }

    /// <summary>
    /// Gets a value indicating whether the catalog /PageMode is set to /UseAttachments, so viewers open the document
    /// with the attachments panel showing. The default is <see langword="false"/>, which leaves /PageMode unchanged.
    /// </summary>
    public bool OpenAttachmentsPanel { get; init; }
}
