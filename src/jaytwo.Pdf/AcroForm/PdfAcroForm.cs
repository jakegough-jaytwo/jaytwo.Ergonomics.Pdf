using System;
using System.Collections.Generic;
using jaytwo.Pdf.Internal;

namespace jaytwo.Pdf.AcroForm;

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
    /// Fills one field by its dotted full name.
    /// </summary>
    /// <remarks>
    /// A text field takes text. A check box takes a Boolean. A radio takes an on-state name, or <see cref="PdfFieldValue.None"/>
    /// to turn every widget off. A choice field takes one export value, a list of export values (more than one only when it
    /// is multi-select), or <see cref="PdfFieldValue.None"/> or an empty list to clear it. WinAnsi text and choice values
    /// get a Helvetica appearance. Check boxes and radios select an existing on-state. A value the library cannot draw
    /// sets /NeedAppearances and returns <see cref="PdfAppearanceDisposition.NeedsViewer"/>. On an unsigned hybrid XFA
    /// form (<see cref="IsHybridXfa"/>) the fill also removes /XFA and the /UR3 usage rights. A refused fill does not
    /// change the document. The change is written by the next save or signature.
    /// </remarks>
    /// <param name="fullName">The dotted field name.</param>
    /// <param name="value">The new value.</param>
    /// <returns>How the widget appearance was produced.</returns>
    /// <exception cref="PdfUnsupportedFormException">The form is XFA-only (<see cref="IsXfaOnly"/>).</exception>
    /// <exception cref="PdfSignatureConflictException">The form is hybrid XFA and already has a signature.</exception>
    /// <exception cref="PdfFieldNotFoundException">No field has the name.</exception>
    /// <exception cref="PdfFieldReadOnlyException">The field is read-only.</exception>
    /// <exception cref="PdfFieldValueException">The field cannot take the value, or it is a password, signature, or push-button field.</exception>
    /// <exception cref="PdfCertificationLockedException">A certification or a FieldMDP lock forbids the change.</exception>
    /// <exception cref="PdfEncryptionRestrictedException">The user password does not allow form filling.</exception>
    public PdfAppearanceDisposition Fill(string fullName, PdfFieldValue value)
    {
        if (fullName == null)
        {
            throw new ArgumentNullException(nameof(fullName));
        }

        return _engine.Fill(new[] { new KeyValuePair<string, PdfFieldValue>(fullName, value) })[fullName];
    }

    /// <summary>
    /// Fills several fields, all or nothing.
    /// </summary>
    /// <remarks>
    /// Every value is checked as <see cref="Fill(string, PdfFieldValue)"/> checks it before any field changes: the name,
    /// the value kind, read-only, /MaxLen, listed options, FieldMDP, certification, and the permission bits. One failure
    /// throws and leaves the document unchanged. The fills then apply in the order given, in one pending revision. On a
    /// hybrid XFA form the set also removes /XFA and /UR3, as a single fill does; an empty set changes nothing.
    /// </remarks>
    /// <param name="values">Full names and values, for example a <see cref="Dictionary{TKey, TValue}"/> or the result of <see cref="Export"/>.</param>
    /// <returns>How each field's appearance was produced, by full name.</returns>
    /// <exception cref="ArgumentException">A name is <see langword="null"/> or appears more than once.</exception>
    /// <exception cref="PdfUnsupportedFormException">The set is not empty and the form is XFA-only (<see cref="IsXfaOnly"/>).</exception>
    /// <exception cref="PdfSignatureConflictException">The set is not empty and the form is hybrid XFA with a signature.</exception>
    /// <exception cref="PdfFieldNotFoundException">No field has one of the names.</exception>
    /// <exception cref="PdfFieldReadOnlyException">One of the fields is read-only.</exception>
    /// <exception cref="PdfFieldValueException">One of the fields cannot take its value.</exception>
    /// <exception cref="PdfCertificationLockedException">A certification or a FieldMDP lock forbids one of the changes.</exception>
    /// <exception cref="PdfEncryptionRestrictedException">The user password does not allow form filling.</exception>
    public IReadOnlyDictionary<string, PdfAppearanceDisposition> Fill(IEnumerable<KeyValuePair<string, PdfFieldValue>> values)
    {
        return _engine.Fill(values);
    }

    /// <summary>
    /// Flattens every field except signature fields: each widget's appearance becomes page content and the field is removed.
    /// </summary>
    /// <remarks>
    /// See <see cref="Flatten(IEnumerable{string})"/>. Signature fields, signed or not, are kept. A form with no other
    /// field is left unchanged.
    /// </remarks>
    /// <exception cref="PdfUnsupportedFormException">The form is XFA-only, /NeedAppearances is true, or a widget has no usable appearance.</exception>
    /// <exception cref="PdfCertificationLockedException">The document is certified, or a FieldMDP lock covers one of the fields.</exception>
    /// <exception cref="PdfSignatureConflictException">The document has a signature and <see cref="PdfDocument.AllowBreakingSignatures"/> is not set.</exception>
    /// <exception cref="PdfEncryptionRestrictedException">The user password does not allow modifying the document and its annotations.</exception>
    public void Flatten()
    {
        _engine.Flatten(null);
    }

    /// <summary>
    /// Flattens the named fields: each widget's appearance becomes page content and the field is removed.
    /// </summary>
    /// <remarks>
    /// Each widget's normal appearance, selected by /AS for a check box or radio, is painted on every page whose /Annots
    /// lists it, placed as ISO 32000-1 12.5.5 places it, inside the widget's optional content when it has one. The page's
    /// existing content streams are wrapped in q and Q and are not read. Hidden and NoView widgets are removed without
    /// being drawn, and a widget draws nothing when its field shows no value and it has no appearance, or its state is Off
    /// and has none. The widgets leave the page /Annots, the fields leave /Fields, their parents' /Kids, and /CO, and a
    /// parent left with no kids is removed too. When no field remains, /XFA and /NeedAppearances are removed. A flatten of a
    /// hybrid XFA form also removes /XFA, and on an unsigned document every flatten removes the /UR3 usage rights. An
    /// unsigned signature field is flattened only when named; a signed one never is. Every check runs before anything
    /// changes, so a refused flatten does not change the document. The change is written by the next save or signature.
    /// </remarks>
    /// <param name="fullNames">The dotted full names. An empty set changes nothing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fullNames"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A name is <see langword="null"/> or appears more than once.</exception>
    /// <exception cref="PdfFieldNotFoundException">No field has one of the names.</exception>
    /// <exception cref="PdfUnsupportedFormException">The form is XFA-only, /NeedAppearances is true, a widget has a value and no usable appearance, or a NoRotate widget is on a rotated page.</exception>
    /// <exception cref="PdfCertificationLockedException">The document is certified, or a FieldMDP lock covers one of the fields.</exception>
    /// <exception cref="PdfSignatureConflictException">A named field is a signed signature field, or the document has a signature and <see cref="PdfDocument.AllowBreakingSignatures"/> is not set.</exception>
    /// <exception cref="PdfEncryptionRestrictedException">The user password does not allow modifying the document and its annotations.</exception>
    public void Flatten(IEnumerable<string> fullNames)
    {
        if (fullNames == null)
        {
            throw new ArgumentNullException(nameof(fullNames));
        }

        _engine.Flatten(fullNames);
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
    /// <see cref="Fill(IEnumerable{KeyValuePair{string, PdfFieldValue}})"/>.
    /// </remarks>
    /// <returns>The values by dotted full name.</returns>
    public IReadOnlyDictionary<string, PdfFieldValue> Export()
    {
        return _engine.Export();
    }
}
