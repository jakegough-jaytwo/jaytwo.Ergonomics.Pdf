using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Ergonomics.Pdf.Internal;
using jaytwo.Pdf;
using jaytwo.Pdf.AcroForm;

namespace jaytwo.Ergonomics.Pdf;

/// <summary>
/// Starts workflows: a set of changes to one existing PDF, described first and run together through <c>jaytwo.Pdf</c>.
/// </summary>
/// <example>
/// <code>
/// await PdfWorkflow.FromFile("order.pdf")
///     .WithTitle("Executed Order")
///     .SaveAsync("order-final.pdf", cancellationToken: cancellationToken);
/// </code>
/// </example>
public static class PdfWorkflow
{
    /// <summary>
    /// Starts a workflow on a readable seekable stream. The stream is borrowed: the workflow reads it only when it runs and never disposes it.
    /// </summary>
    /// <param name="input">The PDF. It must not change until the run ends.</param>
    /// <param name="options">The password, if any.</param>
    /// <returns>The workflow.</returns>
    public static PdfWorkflowBuilder From(Stream input, PdfWorkflowOpenOptions? options = null)
    {
        return new PdfWorkflowBuilder(WorkflowInput.Of(input, options));
    }

    /// <summary>
    /// Starts a workflow on a random-access source. The source is borrowed: the workflow reads it only when it runs and never disposes it.
    /// </summary>
    /// <param name="input">The PDF snapshot. It must not change until the run ends.</param>
    /// <param name="options">The password, if any.</param>
    /// <returns>The workflow.</returns>
    public static PdfWorkflowBuilder From(IPdfSource input, PdfWorkflowOpenOptions? options = null)
    {
        return new PdfWorkflowBuilder(WorkflowInput.Of(input, options));
    }

    /// <summary>
    /// Starts a workflow on a file. The file is opened, shared for reading only, when the workflow runs and closed when the run ends.
    /// </summary>
    /// <param name="path">The PDF's path, resolved against the current directory now.</param>
    /// <param name="options">The password, if any.</param>
    /// <returns>The workflow.</returns>
    public static PdfWorkflowBuilder FromFile(string path, PdfWorkflowOpenOptions? options = null)
    {
        return new PdfWorkflowBuilder(WorkflowInput.OfFile(path, options));
    }

    /// <summary>
    /// Reads a file's form values, as <see cref="PdfAcroForm.Export"/> does, without the caller opening a document (spec F16).
    /// </summary>
    /// <remarks>
    /// The file is opened, shared for reading only, and closed before this returns, after a failure too. It is read as
    /// <see cref="PdfDocument"/> reads it, so a large file is not buffered.
    /// </remarks>
    /// <param name="path">The PDF's path.</param>
    /// <param name="options">The password, if any.</param>
    /// <returns>The values by dotted full name. A file without a form has none.</returns>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <exception cref="ArgumentException">The path is empty.</exception>
    public static IReadOnlyDictionary<string, PdfFieldValue> ExportFormValues(string path, PdfWorkflowOpenOptions? options = null)
    {
        return ExportAsync(path, options, false, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Reads a file's form values, as <see cref="PdfAcroForm.Export"/> does, without the caller opening a document (spec F16).
    /// </summary>
    /// <remarks>
    /// The file is opened, shared for reading only, and closed before the task completes, after a failure or cancellation
    /// too. It is read as <see cref="PdfDocument"/> reads it, so a large file is not buffered. The open awaits its reads;
    /// the engine reads the form itself synchronously (spec F14).
    /// </remarks>
    /// <param name="path">The PDF's path.</param>
    /// <param name="options">The password, if any.</param>
    /// <param name="cancellationToken">Observed while the file opens.</param>
    /// <returns>The values by dotted full name. A file without a form has none.</returns>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <exception cref="ArgumentException">The path is empty.</exception>
    public static Task<IReadOnlyDictionary<string, PdfFieldValue>> ExportFormValuesAsync(string path, PdfWorkflowOpenOptions? options = null, CancellationToken cancellationToken = default)
    {
        return ExportAsync(path, options, true, cancellationToken);
    }

    private static async Task<IReadOnlyDictionary<string, PdfFieldValue>> ExportAsync(string path, PdfWorkflowOpenOptions? options, bool useAsync, CancellationToken cancellationToken)
    {
        WorkflowInput input = WorkflowInput.OfFile(path, options);
        using WorkflowSession session = await input.OpenAsync(false, useAsync, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return session.Document.AcroForm.Export();
    }
}
