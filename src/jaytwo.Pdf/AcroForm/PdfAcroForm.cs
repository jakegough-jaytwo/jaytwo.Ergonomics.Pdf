using System.Collections.Generic;
using jaytwo.Pdf.Internal;

namespace jaytwo.Pdf.AcroForm;

/// <summary>
/// AcroForm fields and form data. <see cref="PdfAcroFormEditor"/> fills and flattens them.
/// </summary>
public sealed class PdfAcroForm
{
    private readonly PdfEngine _engine;

    internal PdfAcroForm(PdfEngine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Gets a value indicating whether the form has any widgets.
    /// </summary>
    public bool HasFields => _engine.HasFields;

    /// <summary>
    /// Gets a value indicating whether /NeedAppearances is true.
    /// </summary>
    public bool NeedAppearances => _engine.NeedAppearances;

    /// <summary>
    /// Gets a value indicating whether the form carries XFA beside AcroForm fields that viewers can show instead.
    /// </summary>
    /// <remarks>
    /// The AcroForm /XFA is a stream or an array, the form has field widgets, and the catalog /NeedsRendering is not
    /// true. XFA-aware viewers show the XFA data, so the first fill of an unsigned hybrid form removes /XFA and the /UR3
    /// usage rights in the same revision, and every viewer then shows the AcroForm values. A fill of a signed hybrid form
    /// throws <see cref="PdfSignatureConflictException"/>. The XFA packets are not read.
    /// </remarks>
    public bool IsHybridXfa => _engine.IsHybridXfa;

    /// <summary>
    /// Gets a value indicating whether the form is defined only by XFA, so a fill cannot change what viewers show.
    /// </summary>
    /// <remarks>
    /// The AcroForm /XFA is a stream or an array, and the catalog /NeedsRendering is true or the form has no field
    /// widgets. Reads work; a fill throws <see cref="PdfUnsupportedFormException"/>. The XFA packets are not read.
    /// </remarks>
    public bool IsXfaOnly => _engine.IsXfaOnly;

    /// <summary>
    /// Gets the widgets in the form.
    /// </summary>
    /// <remarks>Each access reads the document again, pending edits included, and returns a new list; keep the list rather than reading the property in a loop.</remarks>
    public IReadOnlyList<PdfField> Fields => _engine.ListFields();

    /// <summary>
    /// Gets every widget with the dotted full name. Radio buttons return one entry per widget.
    /// </summary>
    /// <param name="fullName">The dotted field name.</param>
    /// <returns>The matching widgets.</returns>
    public IReadOnlyList<PdfField> GetFields(string fullName)
    {
        return _engine.GetFields(fullName);
    }

    /// <summary>
    /// Reads the value of every terminal field: one entry per field, not per widget.
    /// </summary>
    /// <remarks>
    /// A text field is text, empty when it has no value. A check box is a Boolean. A radio is the selected on-state name,
    /// or <see cref="PdfFieldValue.None"/> when no widget is on. A single-select choice field is its export value or
    /// <see cref="PdfFieldValue.None"/>. A multi-select choice field is a list. Signature fields, push buttons, and password
    /// fields hold no form data and are left out. Read-only fields are included but cannot be filled, so leave out the
    /// fields whose <see cref="PdfField.ReadOnly"/> is set before passing an export to
    /// <see cref="PdfAcroFormEditor.Fill(IEnumerable{KeyValuePair{string, PdfFieldValue}})"/>.
    /// </remarks>
    /// <returns>The values by dotted full name.</returns>
    public IReadOnlyDictionary<string, PdfFieldValue> Export()
    {
        return _engine.Export();
    }
}
