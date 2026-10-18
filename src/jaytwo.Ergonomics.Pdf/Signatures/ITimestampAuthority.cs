using System;

namespace jaytwo.Ergonomics.Pdf.Signatures;

/// <summary>
/// Returns an RFC 3161 time-stamp token for a time-stamp request.
/// </summary>
public interface ITimestampAuthority
{
    /// <summary>
    /// Gets a time-stamp token for the supplied request.
    /// </summary>
    /// <param name="timeStampReqDer">A DER-encoded TimeStampReq.</param>
    /// <returns>The TimeStampToken ContentInfo, not the status wrapper.</returns>
    byte[] GetTimeStampToken(ReadOnlySpan<byte> timeStampReqDer);
}
