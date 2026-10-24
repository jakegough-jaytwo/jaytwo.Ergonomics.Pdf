using System;
using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Pdf;

/// <summary>
/// One stable, random-access snapshot of a PDF's bytes (spec R13). The engine reads it at absolute offsets and never writes it.
/// </summary>
/// <remarks>
/// <para>
/// The length and bytes do not change while a document uses the source. A source whose backing store can change detects
/// that and fails the read with <see cref="PdfSourceChangedException"/> instead of returning bytes of another version.
/// </para>
/// <para>
/// A read may return fewer bytes than requested, and zero bytes means the end of the source. The engine does not read one
/// source concurrently. Share a source among documents only when its implementation documents that as safe.
/// </para>
/// <para>
/// Opening asynchronously awaits <see cref="ReadAsync"/> for every read. Synchronous members, including the document's
/// inspection properties and synchronous saves, call <see cref="Read"/>.
/// </para>
/// </remarks>
public interface IPdfSource : IDisposable
{
    /// <summary>
    /// Gets the length of the snapshot in bytes.
    /// </summary>
    long Length { get; }

    /// <summary>
    /// Reads bytes starting at an absolute offset.
    /// </summary>
    /// <param name="position">The offset of the first byte, from the start of the source.</param>
    /// <param name="buffer">Receives the bytes.</param>
    /// <returns>The number of bytes read, at most the length of <paramref name="buffer"/>; zero at the end of the source.</returns>
    /// <exception cref="PdfSourceChangedException">The backing store no longer holds this snapshot.</exception>
    int Read(long position, Span<byte> buffer);

    /// <summary>
    /// Reads bytes starting at an absolute offset.
    /// </summary>
    /// <param name="position">The offset of the first byte, from the start of the source.</param>
    /// <param name="buffer">Receives the bytes.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The number of bytes read, at most the length of <paramref name="buffer"/>; zero at the end of the source.</returns>
    /// <exception cref="PdfSourceChangedException">The backing store no longer holds this snapshot.</exception>
    ValueTask<int> ReadAsync(long position, Memory<byte> buffer, CancellationToken cancellationToken = default);
}
