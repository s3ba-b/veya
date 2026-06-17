using System.Net;
using System.Net.Http;
using System.Text;

namespace Veya.TestSupport;

/// <summary>
/// Stubs an HTTP API call with a canned response while capturing the outgoing
/// request, so backend tests can assert on headers and serialized body instead
/// of only the mapped response.
/// </summary>
public sealed class CapturingHttpMessageHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK, string contentType = "application/json") : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (request.Content is not null)
        {
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, contentType),
        };
    }
}
