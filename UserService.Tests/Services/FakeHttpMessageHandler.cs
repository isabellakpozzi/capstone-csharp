using System.Net;

namespace UserService.Tests.Services;

public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string? _responseBody;
    private readonly Exception? _exceptionToThrow;

    public FakeHttpMessageHandler(HttpStatusCode statusCode, string? responseBody = null)
    {
        _statusCode = statusCode;
        _responseBody = responseBody;
    }

    public FakeHttpMessageHandler(Exception exceptionToThrow)
    {
        _exceptionToThrow = exceptionToThrow;
        _statusCode = HttpStatusCode.InternalServerError;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_exceptionToThrow is not null)
            throw _exceptionToThrow;

        var response = new HttpResponseMessage(_statusCode)
        {
            Content = _responseBody is not null
                ? new StringContent(_responseBody, System.Text.Encoding.UTF8, "application/json")
                : null
        };

        return Task.FromResult(response);
    }
}