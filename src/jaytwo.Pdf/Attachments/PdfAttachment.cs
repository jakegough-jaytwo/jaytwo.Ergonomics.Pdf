using System;
using System.IO;
using System.Text;
using jaytwo.Pdf.Internal;

namespace jaytwo.Pdf.Attachments;

/// <summary>
/// An embedded file in the document name tree. Its name, declared size, and content are untrusted document data.
/// </summary>
public sealed class PdfAttachment
{
    private const int MaximumSafeFileNameLength = 255;
    private const string UnnamedAttachmentFileName = "attachment";

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
    /// Gets the untrusted attachment name: the <c>/EmbeddedFiles</c> name-tree key when that key is a text string,
    /// otherwise the file specification's <c>/UF</c>, then <c>/F</c>. It is not an output path.
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
    /// Gets the untrusted declared /Params /Size, or <see langword="null"/> when the parameter is absent.
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
    /// Produces a portable filename component from the untrusted attachment name.
    /// </summary>
    /// <returns>
    /// A non-empty filename component of at most 255 UTF-16 code units and 255 UTF-8 bytes, with path separators,
    /// control characters, Windows-invalid characters, trailing periods and spaces, and reserved Windows device names
    /// neutralized. The result names no directory and is not guaranteed to be unique among the document's attachments.
    /// </returns>
    public string GetSafeFileName()
    {
        var result = new StringBuilder(Math.Min(Name.Length, MaximumSafeFileNameLength));
        int utf8Length = 0;
        for (int i = 0; i < Name.Length && result.Length < MaximumSafeFileNameLength; i++)
        {
            char value = Name[i];
            if (char.IsHighSurrogate(value) && i + 1 < Name.Length && char.IsLowSurrogate(Name[i + 1]))
            {
                if (result.Length + 2 > MaximumSafeFileNameLength || utf8Length + 4 > MaximumSafeFileNameLength)
                {
                    break;
                }

                result.Append(value);
                result.Append(Name[++i]);
                utf8Length += 4;
            }
            else
            {
                char safeValue = IsUnsafeFileNameCharacter(value) ? '_' : value;
                int encodedLength = Utf8Length(safeValue);
                if (utf8Length + encodedLength > MaximumSafeFileNameLength)
                {
                    break;
                }

                result.Append(safeValue);
                utf8Length += encodedLength;
            }
        }

        string safe = result.ToString().Trim().TrimEnd('.', ' ');
        if (safe.Length == 0 || safe == "." || safe == "..")
        {
            return UnnamedAttachmentFileName;
        }

        if (IsReservedWindowsFileName(safe))
        {
            while (safe.Length > 0
                && (safe.Length >= MaximumSafeFileNameLength || Encoding.UTF8.GetByteCount(safe) >= MaximumSafeFileNameLength))
            {
                int remove = safe.Length >= 2 && char.IsSurrogatePair(safe, safe.Length - 2) ? 2 : 1;
                safe = safe.Substring(0, safe.Length - remove);
            }

            safe = "_" + safe;
        }

        return safe;
    }

    /// <summary>
    /// Opens the decoded, untrusted file bytes without a caller-selected size limit. The caller disposes the stream.
    /// </summary>
    /// <returns>A readable stream of the attachment.</returns>
    public Stream Open()
    {
        return _engine.OpenAttachment(Name);
    }

    /// <summary>
    /// Opens the decoded file bytes with a caller-selected decoded-byte limit. The declared <see cref="Size"/> is not
    /// trusted or used to enforce the limit. The caller disposes the stream.
    /// </summary>
    /// <param name="maxDecodedBytes">The maximum number of decoded bytes that the stream may return.</param>
    /// <returns>A readable stream of the attachment.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxDecodedBytes"/> is negative.</exception>
    /// <exception cref="PdfAttachmentTooLargeException">The decoded attachment exceeds the limit.</exception>
    public Stream Open(long maxDecodedBytes)
    {
        if (maxDecodedBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDecodedBytes));
        }

        return new PdfDecodedByteLimitStream(_engine.OpenAttachment(Name), Name, maxDecodedBytes);
    }

    private static bool IsReservedWindowsFileName(string value)
    {
        int period = value.IndexOf('.');
        string stem = (period < 0 ? value : value.Substring(0, period)).TrimEnd(' ', '.');
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return stem.Length == 4
            && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
            && ((stem[3] >= '1' && stem[3] <= '9') || stem[3] == '\u00B9' || stem[3] == '\u00B2' || stem[3] == '\u00B3');
    }

    private static bool IsUnsafeFileNameCharacter(char value)
    {
        return value <= 31
            || value == 127
            || char.IsSurrogate(value)
            || value == '<'
            || value == '>'
            || value == ':'
            || value == '"'
            || value == '/'
            || value == '\\'
            || value == '|'
            || value == '?'
            || value == '*';
    }

    private static int Utf8Length(char value)
    {
        if (value <= 0x7F)
        {
            return 1;
        }

        return value <= 0x7FF ? 2 : 3;
    }
}
