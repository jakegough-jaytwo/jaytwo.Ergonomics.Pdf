namespace jaytwo.Ergonomics.Pdf.Attachments;

/// <summary>
/// How an embedded file relates to the document, stored as the file specification's /AFRelationship
/// (ISO 32000-2 Table 43). PDF/A-3 allows <see cref="Source"/>, <see cref="Data"/>, <see cref="Alternative"/>,
/// <see cref="Supplement"/>, and <see cref="Unspecified"/>; the other values come from PDF 2.0, and a PDF/A-3 file is
/// not promised to stay conformant when one of them is written. No PDF/A, Factur-X, or ZUGFeRD conformance is claimed.
/// </summary>
public enum PdfAttachmentRelationship
{
    /// <summary>
    /// The relationship is not known or not one of the others. This is the default.
    /// </summary>
    Unspecified,

    /// <summary>
    /// The original source material for the document content.
    /// </summary>
    Source,

    /// <summary>
    /// Information used to derive a visual presentation, such as a table or a graph.
    /// </summary>
    Data,

    /// <summary>
    /// An alternative representation of the content, such as the XML of an e-invoice.
    /// </summary>
    Alternative,

    /// <summary>
    /// A supplemental representation of the original source or data that may be easier to process.
    /// </summary>
    Supplement,

    /// <summary>
    /// An encrypted payload document that the document wraps (PDF 2.0).
    /// </summary>
    EncryptedPayload,

    /// <summary>
    /// Data associated with the document's form fields (PDF 2.0).
    /// </summary>
    FormData,

    /// <summary>
    /// A schema definition for the associated object (PDF 2.0).
    /// </summary>
    Schema,
}
