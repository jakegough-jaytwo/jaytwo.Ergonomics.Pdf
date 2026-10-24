using System.IO;
using jaytwo.Ergonomics.Pdf.Internal;
using jaytwo.Pdf;

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
}
