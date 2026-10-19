using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Ergonomics.Pdf.Signatures;

/// <summary>
/// Posts an RFC 3161 request to an HTTP timestamp authority.
/// </summary>
public sealed class HttpTimestampAuthority : ITimestampAuthority
{
    private const int MaxResponseBytes = 2097152;
    private const int ReadBufferBytes = 81920;

    private readonly HttpClient _httpClient;
    private readonly Uri _endpoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpTimestampAuthority"/> class.
    /// </summary>
    /// <param name="httpClient">The client that sends the request. The caller owns its lifetime.</param>
    /// <param name="endpoint">The timestamp authority URL.</param>
    public HttpTimestampAuthority(HttpClient httpClient, Uri endpoint)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
    }

    /// <summary>
    /// Posts the request and returns the embedded time-stamp token.
    /// </summary>
    /// <param name="timeStampReqDer">A DER-encoded TimeStampReq.</param>
    /// <returns>The TimeStampToken ContentInfo.</returns>
    /// <exception cref="PdfTimestampException">The response is not HTTP success, is larger than 2 MiB, or does not carry a granted token.</exception>
    public byte[] GetTimeStampToken(ReadOnlySpan<byte> timeStampReqDer)
    {
        byte[] body = ExchangeAsync(timeStampReqDer.ToArray()).ConfigureAwait(false).GetAwaiter().GetResult();
        return ExtractToken(body);
    }

    private static async Task<byte[]> ReadCappedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaxResponseBytes)
        {
            throw TooLarge();
        }

        using Stream responseBody = await content.ReadAsStreamAsync().ConfigureAwait(false);
        using var copy = new MemoryStream();
        byte[] buffer = new byte[ReadBufferBytes];
        int total = 0;
        while (true)
        {
            int read = await responseBody.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return copy.ToArray();
            }

            if (read > MaxResponseBytes - total)
            {
                throw TooLarge();
            }

            copy.Write(buffer, 0, read);
            total += read;
        }
    }

    private static PdfTimestampException TooLarge()
    {
        return new PdfTimestampException("The timestamp response is larger than " + MaxResponseBytes.ToString(System.Globalization.CultureInfo.InvariantCulture) + " bytes.");
    }

    private static byte[] ExtractToken(byte[] body)
    {
        DerCodec.DerTlv response = DerCodec.ReadTlv(body);
        if (response.Tag != 0x30)
        {
            throw new PdfTimestampException("The timestamp response is not a DER sequence.");
        }

        DerCodec.DerTlv statusInfo = DerCodec.ReadTlv(response.Content);
        DerCodec.DerTlv status = DerCodec.ReadTlv(statusInfo.Content);
        int code = DerCodec.ReadInteger(status);
        if (code < 0 || code > 1)
        {
            throw new PdfTimestampException("The timestamp authority rejected the request with status " + code.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
        }

        int offset = statusInfo.Full.Length;
        if (offset >= response.Content.Length)
        {
            throw new PdfTimestampException("The timestamp response did not include a token.");
        }

        DerCodec.DerTlv token = DerCodec.ReadTlv(response.Content, offset);
        if (token.Tag != 0x30)
        {
            throw new PdfTimestampException("The timestamp token is not a ContentInfo.");
        }

        return token.Full;
    }

    private async Task<byte[]> ExchangeAsync(byte[] timeStampReqDer)
    {
        // HttpClient times the header phase itself and throws its own timeout exception.
        // ResponseHeadersRead keeps it from buffering the body, so this token, started
        // before the send, keeps the body read under the same Timeout. Infinite stays infinite.
        using var timeout = new CancellationTokenSource(_httpClient.Timeout);
        using var content = new ByteArrayContent(timeStampReqDer);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/timestamp-query");
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = content };
        using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new PdfTimestampException("The timestamp authority returned HTTP " + ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
        }

        // Disposing the response aborts a body stream that ignores the token.
        using CancellationTokenRegistration abort = timeout.Token.Register(response.Dispose);
        try
        {
            return await ReadCappedAsync(response.Content, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (timeout.IsCancellationRequested && !(exception is PdfTimestampException))
        {
            string message = "The timestamp response was canceled because the configured HttpClient.Timeout of "
                + _httpClient.Timeout.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " seconds elapsed.";
            throw new TaskCanceledException(message, new TimeoutException(message, exception));
        }
    }
}
