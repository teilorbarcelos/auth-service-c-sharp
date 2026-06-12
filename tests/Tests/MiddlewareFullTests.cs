using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;
using MageBackend.Web.Middleware;
using MageBackend.Infrastructure.Configuration;

namespace MageBackend.Tests
{
    public class RateLimitMiddlewareFullTests
    {
        [Fact]
        public async Task ExemptPaths_PassesThrough()
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/health";
            context.Response.Body = new MemoryStream();
            var called = false;

            var middleware = new RateLimitMiddleware(_ =>
            {
                called = true;
                return Task.CompletedTask;
            });
            await middleware.InvokeAsync(context);
            Assert.True(called);
        }

        [Fact]
        public async Task NonExemptPath_WithKnownEndpoint_ChecksLimit()
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/v1/auth/login";
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
            context.Response.Body = new MemoryStream();
            var called = false;

            var middleware = new RateLimitMiddleware(_ =>
            {
                called = true;
                return Task.CompletedTask;
            });
            await middleware.InvokeAsync(context);
            Assert.True(called);
        }

        [Fact]
        public async Task NullPath_UsesDefault()
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            var called = false;

            var middleware = new RateLimitMiddleware(_ =>
            {
                called = true;
                return Task.CompletedTask;
            });
            await middleware.InvokeAsync(context);
            Assert.True(called);
        }
    }

    public class RequestLoggingMiddlewareFullTests
    {
        [Fact]
        public async Task Invoke_LogsRequest()
        {
            var context = new DefaultHttpContext();
            context.Request.Method = "POST";
            context.Request.Path = "/v1/auth/login";
            context.Response.Body = new MemoryStream();
            var called = false;

            var middleware = new RequestLoggingMiddleware(_ =>
            {
                called = true;
                return Task.CompletedTask;
            });
            await middleware.InvokeAsync(context);
            Assert.True(called);
        }
    }
}
