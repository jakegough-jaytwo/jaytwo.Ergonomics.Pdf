using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace jaytwo.Pdf.Signatures;

/// <summary>
/// Describes an unsuccessful attempt to restore a signing destination after an output failure.
/// </summary>
/// <remarks>
/// The original exception is rethrown. This information is attached to its <see cref="Exception.Data"/>
/// under <see cref="DataKey"/> when that dictionary accepts it, and is always available through
/// <see cref="Get"/>. Default exception logging may omit this information. Restored pending edits do not
/// establish that a physical destination is safe for retry.
/// </remarks>
[Serializable]
public sealed class PdfSigningRollbackFailure
{
    /// <summary>
    /// The key used to attach this information to the original exception's data dictionary.
    /// </summary>
    public const string DataKey = "jaytwo.Pdf.Signatures.RollbackFailure";

    private static readonly ConditionalWeakTable<Exception, PdfSigningRollbackFailure> Failures = new();

    private PdfSigningRollbackFailure(long originalLength, bool lengthRestored, Exception restorationException)
    {
        OriginalLength = originalLength;
        LengthRestored = lengthRestored;
        RestorationException = restorationException;
    }

    /// <summary>
    /// Gets the length to which the destination was to be restored.
    /// </summary>
    public long OriginalLength { get; }

    /// <summary>
    /// Gets whether setting the destination length succeeded. False means its final length is uncertain.
    /// </summary>
    public bool LengthRestored { get; }

    /// <summary>
    /// Gets whether seeking to <see cref="OriginalLength"/> was attempted, after successful truncation.
    /// </summary>
    public bool SeekAttempted => LengthRestored;

    /// <summary>
    /// Gets whether seeking succeeded. It is false for every unsuccessful restoration reported here.
    /// </summary>
    public bool SeekSucceeded => false;

    /// <summary>
    /// Gets the exception from setting the length, or from seeking after successful truncation.
    /// </summary>
    public Exception RestorationException { get; }

    /// <summary>
    /// Gets signing restoration failure information for an original operation exception, if present.
    /// This also works when the exception's data dictionary refuses writes.
    /// </summary>
    /// <param name="exception">The original operation exception.</param>
    /// <returns>The restoration failure, or null when none was recorded.</returns>
    public static PdfSigningRollbackFailure? Get(Exception exception)
    {
        if (exception == null)
        {
            throw new ArgumentNullException(nameof(exception));
        }

        return Failures.TryGetValue(exception, out PdfSigningRollbackFailure? failure) ? failure : null;
    }

    internal static void Restore(Stream destination, long originalLength, Exception original)
    {
        bool lengthRestored = false;
        try
        {
            destination.SetLength(originalLength);
            lengthRestored = true;
            destination.Seek(originalLength, SeekOrigin.Begin);
        }
        catch (Exception restorationException)
        {
            var failure = new PdfSigningRollbackFailure(originalLength, lengthRestored, restorationException);
            lock (Failures)
            {
                Failures.Remove(original);
                Failures.Add(original, failure);
            }

            try
            {
                original.Data[DataKey] = failure;
            }
            catch (Exception)
            {
                // A custom exception can expose a read-only or throwing Data dictionary. Get still works,
                // and diagnostics must not replace the original operation failure.
            }
        }
    }
}
