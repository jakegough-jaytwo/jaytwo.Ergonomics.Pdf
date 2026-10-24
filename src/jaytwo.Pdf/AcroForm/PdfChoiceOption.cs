using System;

namespace jaytwo.Pdf.AcroForm;

/// <summary>
/// One entry from a choice field /Opt array.
/// </summary>
public readonly record struct PdfChoiceOption
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfChoiceOption"/> struct.
    /// </summary>
    /// <param name="value">The export value stored in /V.</param>
    /// <param name="display">The text shown for this entry.</param>
    public PdfChoiceOption(string value, string display)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Display = display ?? throw new ArgumentNullException(nameof(display));
    }

    /// <summary>
    /// Gets the export value stored in /V.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the text shown to the user. This matches <see cref="Value"/> when /Opt contains strings rather than pairs.
    /// </summary>
    public string Display { get; }
}
