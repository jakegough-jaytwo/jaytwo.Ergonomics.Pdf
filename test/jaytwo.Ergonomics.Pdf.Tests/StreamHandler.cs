using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Ergonomics.Pdf.Tests;

internal sealed class StreamHandler : HttpMessageHandler
{
    private readonly Stream _body;
    private readonly long? _declaredLength;

    public StreamHandler(Stream body, long? declaredLength = null)
    {
        _body = body;
        _declaredLength = declaredLength;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var content = new StreamContent(_body);
        if (_declaredLength.HasValue)
        {
            content.Headers.ContentLength = _declaredLength;
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }
}
