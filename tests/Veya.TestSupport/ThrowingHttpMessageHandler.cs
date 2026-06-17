using System.Net.Http;

namespace Veya.TestSupport;

/// <summary>Simulates a network failure (host unreachable, connection refused) for backend tests.</summary>
public sealed class ThrowingHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new HttpRequestException("Connection refused");
}
