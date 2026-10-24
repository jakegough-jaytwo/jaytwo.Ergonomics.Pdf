namespace jaytwo.Pdf.AcroForm;

/// <summary>
/// The kind of button represented by a /Btn field.
/// </summary>
public enum PdfButtonKind
{
    /// <summary>
    /// The field is not a button.
    /// </summary>
    None,

    /// <summary>
    /// A check box.
    /// </summary>
    CheckBox,

    /// <summary>
    /// A radio button widget.
    /// </summary>
    Radio,

    /// <summary>
    /// A push button.
    /// </summary>
    Push,
}
