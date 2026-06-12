using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using MageBackend.Web.Middleware;

namespace MageBackend.Tests
{
    public class RequestLoggingMiddlewareTests
    {
        [Fact]
        public async Task Invoke_CallsNext()
        {
            var context = new DefaultHttpContext();
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

    public class RateLimitMiddlewareTests
    {
        [Fact]
        public async Task Invoke_CallsNext()
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
}
