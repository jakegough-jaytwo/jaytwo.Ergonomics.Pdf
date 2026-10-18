using System.Collections.Generic;
using jaytwo.Ergonomics.Pdf.Internal;

namespace jaytwo.Ergonomics.Pdf.AcroForm;

/// <summary>
/// AcroForm fields and fill operations.
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
    /// Gets the widgets in the form.
    /// </summary>
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
    /// Sets a text field value and generates a Helvetica appearance when the value is simple enough.
    /// </summary>
    /// <param name="fullName">The dotted field name.</param>
    /// <param name="value">The new value.</param>
    /// <returns>Whether an appearance was generated.</returns>
    public PdfAppearanceDisposition SetText(string fullName, string value)
    {
        return _engine.SetText(fullName, value);
    }

    /// <summary>
    /// Sets a check box by selecting its existing on-state appearance when one exists.
    /// </summary>
    /// <param name="fullName">The dotted field name.</param>
    /// <param name="isChecked">True to select the on-state.</param>
    /// <returns>Whether an existing appearance was selected.</returns>
    public PdfAppearanceDisposition SetCheckBox(string fullName, bool isChecked)
    {
        return _engine.SetCheckBox(fullName, isChecked);
    }

    /// <summary>
    /// Sets a radio group to the widget whose on-state matches <paramref name="exportValue"/>.
    /// </summary>
    /// <param name="fullName">The dotted field name.</param>
    /// <param name="exportValue">The on-state name, without a leading slash.</param>
    /// <returns>Whether an existing appearance was selected.</returns>
    public PdfAppearanceDisposition SetRadio(string fullName, string exportValue)
    {
        return _engine.SetRadio(fullName, exportValue);
    }

    /// <summary>
    /// Sets a choice field to the export values and requests viewer appearances.
    /// </summary>
    /// <param name="fullName">The dotted field name.</param>
    /// <param name="values">The export values. A multi-select field accepts more than one and is stored as a PDF array.</param>
    /// <returns>Always <see cref="PdfAppearanceDisposition.NeedAppearances"/>.</returns>
    public PdfAppearanceDisposition SetChoice(string fullName, params string[] values)
    {
        return _engine.SetChoice(fullName, values);
    }
}
