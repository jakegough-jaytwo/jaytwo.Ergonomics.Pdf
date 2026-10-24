using System;
using System.Collections.Generic;

namespace jaytwo.Pdf.AcroForm;

/// <summary>
/// A form field value: text, a Boolean, a list of choice values, or no value.
/// </summary>
/// <remarks>
/// A text field takes text. A check box takes a Boolean. A radio takes the on-state name of the widget to select, or no
/// value to turn every widget off. A choice field takes one export value, a list of export values (more than one only
/// when the field is multi-select), or no value or an empty list to clear it. The default value is <see cref="None"/>.
/// </remarks>
public readonly struct PdfFieldValue : IEquatable<PdfFieldValue>
{
    private readonly string? _text;
    private readonly bool _boolean;
    private readonly string[]? _list;

    private PdfFieldValue(PdfFieldValueKind kind, string? text, bool boolean, string[]? list)
    {
        Kind = kind;
        _text = text;
        _boolean = boolean;
        _list = list;
    }

    /// <summary>
    /// Gets the value that holds nothing. It is the default of this type.
    /// </summary>
    public static PdfFieldValue None => default;

    /// <summary>
    /// Gets the kind of value held.
    /// </summary>
    public PdfFieldValueKind Kind { get; }

    /// <summary>
    /// Gets the string when <see cref="Kind"/> is <see cref="PdfFieldValueKind.Text"/>; otherwise <see langword="null"/>.
    /// </summary>
    public string? Text => Kind == PdfFieldValueKind.Text ? _text : null;

    /// <summary>
    /// Gets the Boolean when <see cref="Kind"/> is <see cref="PdfFieldValueKind.Boolean"/>; otherwise <see langword="null"/>.
    /// </summary>
    public bool? Boolean => Kind == PdfFieldValueKind.Boolean ? _boolean : null;

    /// <summary>
    /// Gets the strings when <see cref="Kind"/> is <see cref="PdfFieldValueKind.List"/>; otherwise an empty list.
    /// </summary>
    public IReadOnlyList<string> Values => _list ?? Array.Empty<string>();

    /// <summary>
    /// Converts a string to a text value. A <see langword="null"/> string is <see cref="None"/>.
    /// </summary>
    /// <param name="value">The string.</param>
    public static implicit operator PdfFieldValue(string? value)
    {
        return FromText(value);
    }

    /// <summary>
    /// Converts a Boolean to a check-box value.
    /// </summary>
    /// <param name="value">True for checked.</param>
    public static implicit operator PdfFieldValue(bool value)
    {
        return FromBoolean(value);
    }

    /// <summary>
    /// Converts an array to a list of choice values. A <see langword="null"/> array is <see cref="None"/>.
    /// </summary>
    /// <param name="values">The choice export values.</param>
    public static implicit operator PdfFieldValue(string[]? values)
    {
        return FromList(values);
    }

    /// <summary>
    /// Compares two values.
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both hold the same kind and content.</returns>
    public static bool operator ==(PdfFieldValue left, PdfFieldValue right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Compares two values.
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the kinds or contents differ.</returns>
    public static bool operator !=(PdfFieldValue left, PdfFieldValue right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Creates a text value.
    /// </summary>
    /// <param name="value">The string. <see langword="null"/> gives <see cref="None"/>.</param>
    /// <returns>The value.</returns>
    public static PdfFieldValue FromText(string? value)
    {
        return value == null ? None : new PdfFieldValue(PdfFieldValueKind.Text, value, false, null);
    }

    /// <summary>
    /// Creates a check-box value.
    /// </summary>
    /// <param name="value">True for checked.</param>
    /// <returns>The value.</returns>
    public static PdfFieldValue FromBoolean(bool value)
    {
        return new PdfFieldValue(PdfFieldValueKind.Boolean, null, value, null);
    }

    /// <summary>
    /// Creates a list of choice values. The strings are copied.
    /// </summary>
    /// <param name="values">The choice export values. <see langword="null"/> gives <see cref="None"/>.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentException">An item is <see langword="null"/>.</exception>
    public static PdfFieldValue FromList(IEnumerable<string>? values)
    {
        if (values == null)
        {
            return None;
        }

        var copy = new List<string>(values);
        foreach (string? item in copy)
        {
            if (item == null)
            {
                throw new ArgumentException("A list of field values cannot contain null.", nameof(values));
            }
        }

        return new PdfFieldValue(PdfFieldValueKind.List, null, false, copy.ToArray());
    }

    /// <inheritdoc/>
    public bool Equals(PdfFieldValue other)
    {
        if (Kind != other.Kind)
        {
            return false;
        }

        switch (Kind)
        {
            case PdfFieldValueKind.Text:
                return string.Equals(_text, other._text, StringComparison.Ordinal);
            case PdfFieldValueKind.Boolean:
                return _boolean == other._boolean;
            case PdfFieldValueKind.List:
                if (Values.Count != other.Values.Count)
                {
                    return false;
                }

                for (int i = 0; i < Values.Count; i++)
                {
                    if (!string.Equals(Values[i], other.Values[i], StringComparison.Ordinal))
                    {
                        return false;
                    }
                }

                return true;
            default:
                return true;
        }
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is PdfFieldValue other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        int hash = (int)Kind;
        switch (Kind)
        {
            case PdfFieldValueKind.Text:
                return (hash * 31) + StringComparer.Ordinal.GetHashCode(_text!);
            case PdfFieldValueKind.Boolean:
                return (hash * 31) + (_boolean ? 1 : 0);
            case PdfFieldValueKind.List:
                foreach (string item in Values)
                {
                    hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(item);
                }

                return hash;
            default:
                return hash;
        }
    }

    /// <summary>
    /// Formats the value for diagnostics.
    /// </summary>
    /// <returns>The text, <c>true</c> or <c>false</c>, the list in brackets, or <c>(none)</c>.</returns>
    public override string ToString()
    {
        return Kind switch
        {
            PdfFieldValueKind.Text => _text!,
            PdfFieldValueKind.Boolean => _boolean ? "true" : "false",
            PdfFieldValueKind.List => "[" + string.Join(", ", Values) + "]",
            _ => "(none)",
        };
    }
}
