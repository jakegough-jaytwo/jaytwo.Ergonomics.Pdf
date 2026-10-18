using System;
using System.Collections.Generic;

namespace jaytwo.Ergonomics.Pdf.AcroForm;

/// <summary>
/// One AcroForm widget, including inherited field values and page geometry.
/// </summary>
public sealed class PdfField
{
    internal PdfField(
        string partialName,
        string fullName,
        int widgetIndex,
        PdfFieldType fieldType,
        PdfButtonKind buttonKind,
        IReadOnlyList<string> selectedValues,
        string? exportValue,
        PdfRect rect,
        int pageNumber,
        int pageRotation,
        PdfRect mediaBox,
        bool readOnly,
        bool required,
        bool multiline,
        int? maxLength,
        bool editable,
        bool multiSelect,
        IReadOnlyList<PdfChoiceOption> options)
    {
        PartialName = partialName;
        FullName = fullName;
        WidgetIndex = widgetIndex;
        FieldType = fieldType;
        ButtonKind = buttonKind;
        SelectedValues = selectedValues ?? throw new ArgumentNullException(nameof(selectedValues));
        Value = SelectedValues.Count == 0 ? null : SelectedValues[0];
        ExportValue = exportValue;
        Rect = rect;
        PageNumber = pageNumber;
        PageRotation = pageRotation;
        MediaBox = mediaBox;
        ReadOnly = readOnly;
        Required = required;
        Multiline = multiline;
        MaxLength = maxLength;
        Editable = editable;
        MultiSelect = multiSelect;
        Options = options ?? throw new ArgumentNullException(nameof(options));
        IsChecked = buttonKind switch
        {
            PdfButtonKind.CheckBox => Value != null && Value != "Off",
            PdfButtonKind.Radio => exportValue != null && Value == exportValue,
            _ => null,
        };
    }

    /// <summary>
    /// Gets the partial field name from this widget or its nearest named ancestor.
    /// </summary>
    public string PartialName { get; }

    /// <summary>
    /// Gets the dotted full field name.
    /// </summary>
    public string FullName { get; }

    /// <summary>
    /// Gets the zero-based index of this widget among widgets that share <see cref="FullName"/>.
    /// </summary>
    public int WidgetIndex { get; }

    /// <summary>
    /// Gets the field type.
    /// </summary>
    public PdfFieldType FieldType { get; }

    /// <summary>
    /// Gets the button kind, or <see cref="PdfButtonKind.None"/> when the field is not a button.
    /// </summary>
    public PdfButtonKind ButtonKind { get; }

    /// <summary>
    /// Gets the field value, or <see langword="null"/> when it is empty or is a dictionary.
    /// </summary>
    /// <remarks>
    /// Choice fields with several selected entries return the first one. Use <see cref="SelectedValues"/> for the full list.
    /// Check boxes and radios return the PDF name, which is <c>Off</c> or the widget's on-state. Use <see cref="IsChecked"/>.
    /// </remarks>
    public string? Value { get; }

    /// <summary>
    /// Gets every selected value. Text fields have at most one. Multi-select choice fields have one entry per /V item.
    /// </summary>
    public IReadOnlyList<string> SelectedValues { get; }

    /// <summary>
    /// Gets the appearance on-state name for a check box or radio widget.
    /// </summary>
    public string? ExportValue { get; }

    /// <summary>
    /// Gets whether a check box or radio widget is selected. <see langword="null"/> for every other field type.
    /// </summary>
    /// <remarks>
    /// A check box is checked when /V is a name other than Off. A radio widget is checked when the group value equals <see cref="ExportValue"/>.
    /// </remarks>
    public bool? IsChecked { get; }

    /// <summary>
    /// Gets the choice field /Opt entries. Empty when the field is not a choice field or /Opt is absent.
    /// </summary>
    public IReadOnlyList<PdfChoiceOption> Options { get; }

    /// <summary>
    /// Gets the widget rectangle in PDF user space.
    /// </summary>
    public PdfRect Rect { get; }

    /// <summary>
    /// Gets the 1-based page number that contains the widget.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Gets the page rotation in degrees.
    /// </summary>
    public int PageRotation { get; }

    /// <summary>
    /// Gets the page media box, including a box inherited from an ancestor pages node.
    /// </summary>
    public PdfRect MediaBox { get; }

    /// <summary>
    /// Gets a value indicating whether the field is read-only.
    /// </summary>
    public bool ReadOnly { get; }

    /// <summary>
    /// Gets a value indicating whether the Required flag is set.
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// Gets a value indicating whether the text field is multiline.
    /// </summary>
    public bool Multiline { get; }

    /// <summary>
    /// Gets the text field maximum length, or <see langword="null"/> when /MaxLen is absent.
    /// </summary>
    public int? MaxLength { get; }

    /// <summary>
    /// Gets a value indicating whether the choice field Edit flag is set, which allows a value that is not in <see cref="Options"/>.
    /// </summary>
    public bool Editable { get; }

    /// <summary>
    /// Gets a value indicating whether the choice field allows more than one selected value.
    /// </summary>
    public bool MultiSelect { get; }
}
