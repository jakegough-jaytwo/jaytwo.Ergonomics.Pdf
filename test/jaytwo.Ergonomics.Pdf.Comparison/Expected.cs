using System;
using System.Globalization;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Comparison;

// What both libraries must read from a fixture field, so each one does the same work inside the measured window.
internal static class Expected
{
    // A WriteDense field: fieldN holds "value N" on page N.
    public static void DenseField(string name, string? value, int page)
    {
        Assert.StartsWith("field", name, StringComparison.Ordinal);
        string number = name.Substring("field".Length);
        Assert.Equal("value " + number, value);
        Assert.Equal(int.Parse(number, CultureInfo.InvariantCulture), page);
    }

    // A WriteForm field on page N: pageN.name holds "Name N", and pageN.agree is checked on even pages.
    public static void FormField(string name, string? text, bool isChecked, int page)
    {
        Assert.StartsWith("page", name, StringComparison.Ordinal);
        int dot = name.IndexOf('.');
        int number = int.Parse(name.Substring("page".Length, dot - "page".Length), CultureInfo.InvariantCulture);
        Assert.Equal(number, page);
        if (name.Substring(dot + 1) == "name")
        {
            Assert.Equal("Name " + number.ToString(CultureInfo.InvariantCulture), text);
        }
        else
        {
            Assert.Equal("agree", name.Substring(dot + 1));
            Assert.Equal(number % 2 == 0, isChecked);
        }
    }
}
