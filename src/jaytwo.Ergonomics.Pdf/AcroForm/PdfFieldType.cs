namespace jaytwo.Ergonomics.Pdf.AcroForm;

/// <summary>
/// The AcroForm field type stored in /FT.
/// </summary>
public enum PdfFieldType
{
    /// <summary>
    /// A text field, /Tx.
    /// </summary>
    Text,

    /// <summary>
    /// A button field, /Btn.
    /// </summary>
    Button,

    /// <summary>
    /// A choice field, /Ch.
    /// </summary>
    Choice,

    /// <summary>
    /// A signature field, /Sig.
    /// </summary>
    Signature,
}
