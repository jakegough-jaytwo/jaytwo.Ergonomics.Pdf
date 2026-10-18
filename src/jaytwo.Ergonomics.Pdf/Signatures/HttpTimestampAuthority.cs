using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace jaytwo.Ergonomics.Pdf.Signatures;

/// <summary>
/// Posts an RFC 3161 request to an HTTP timestamp authority.
/// </summary>
public sealed class HttpTimestampAuthority : ITimestampAuthority
{
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
    public byte[] GetTimeStampToken(ReadOnlySpan<byte> timeStampReqDer)
    {
        using var content = new ByteArrayContent(timeStampReqDer.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/timestamp-query");
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = content };
        using HttpResponseMessage response = Post(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new PdfTimestampException("The timestamp authority returned HTTP " + ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
        }

        using Stream responseBody = OpenResponse(response);
        using var copy = new MemoryStream();
        responseBody.CopyTo(copy);
        return ExtractToken(copy.ToArray());
    }

    private static Stream OpenResponse(HttpResponseMessage response)
    {
        return response.Content.ReadAsStreamAsync().ConfigureAwait(false).GetAwaiter().GetResult();
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

    private HttpResponseMessage Post(HttpRequestMessage request)
    {
        return _httpClient.SendAsync(request).ConfigureAwait(false).GetAwaiter().GetResult();
    }
}
