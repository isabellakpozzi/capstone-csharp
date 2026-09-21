using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using UserService.Middleware;
using UserService.Models.Dtos;
using Xunit;

namespace UserService.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<ErrorResponse> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<ErrorResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
    }

    [Fact]
    public async Task InvokeAsync_WhenNoExceptionThrown_PassesThroughUnchanged()
    {
        var context = CreateContext();
        var middleware = new ExceptionHandlingMiddleware(
            _ => Task.CompletedTask,
            Mock.Of<ILogger<ExceptionHandlingMiddleware>>());

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(200); // default, untouched
    }

    [Fact]
    public async Task InvokeAsync_WhenUnauthorizedAccessExceptionThrown_Returns401WithErrorResponse()
    {
        var context = CreateContext();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new UnauthorizedAccessException("bad claim"),
            Mock.Of<ILogger<ExceptionHandlingMiddleware>>());

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(401);
        var body = await ReadBodyAsync(context);
        body.Error.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task InvokeAsync_WhenGenericExceptionThrown_Returns500WithErrorResponse()
    {
        var context = CreateContext();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("something broke"),
            Mock.Of<ILogger<ExceptionHandlingMiddleware>>());

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(500);
        var body = await ReadBodyAsync(context);
        body.Error.Should().Be("INTERNAL_SERVER_ERROR");
    }
}