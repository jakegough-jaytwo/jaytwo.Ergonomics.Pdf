using System;

namespace jaytwo.Pdf;

/// <summary>
/// Base type for failures while reading or updating a PDF.
/// </summary>
public class PdfException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public PdfException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public PdfException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
