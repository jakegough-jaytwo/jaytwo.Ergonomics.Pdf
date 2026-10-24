using System;
using System.Collections.Generic;
using jaytwo.Pdf.AcroForm;

namespace jaytwo.Pdf.Internal;

internal sealed class FieldSlot
{
    public string PartialName { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public int WidgetIndex { get; set; }

    public int ValueObjectId { get; set; }

    public int WidgetObjectId { get; set; }

    public PdfFieldType FieldType { get; set; }

    public PdfButtonKind ButtonKind { get; set; }

    public string? Value { get; set; }

    public string? ExportValue { get; set; }

    public PdfRect Rect { get; set; }

    public int PageNumber { get; set; }

    public int PageRotation { get; set; }

    public PdfRect MediaBox { get; set; }

    public bool ReadOnly { get; set; }

    public bool Required { get; set; }

    public bool Multiline { get; set; }

    public int? MaxLength { get; set; }

    public bool Password { get; set; }

    public bool FileSelect { get; set; }

    public bool Comb { get; set; }

    public bool RichText { get; set; }

    public bool Editable { get; set; }

    public bool MultiSelect { get; set; }

    public IReadOnlyList<string> SelectedValues { get; set; } = Array.Empty<string>();

    public IReadOnlyList<PdfChoiceOption> Options { get; set; } = Array.Empty<PdfChoiceOption>();

    public string? DefaultAppearance { get; set; }

    public int Align { get; set; }
}
