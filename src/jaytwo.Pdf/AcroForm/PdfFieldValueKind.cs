namespace jaytwo.Pdf.AcroForm;

/// <summary>
/// The kind of value a <see cref="PdfFieldValue"/> holds.
/// </summary>
public enum PdfFieldValueKind
{
    /// <summary>
    /// No value: a radio with no selection or a single-select choice field with no value. Filling it clears the field.
    /// </summary>
    None,

    /// <summary>
    /// A string: a text value, a radio on-state name, or one choice export value.
    /// </summary>
    Text,

    /// <summary>
    /// A Boolean: whether a check box is checked.
    /// </summary>
    Boolean,

    /// <summary>
    /// A list of choice export values.
    /// </summary>
    List,
}
