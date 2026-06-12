using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using MageBackend.Database;
using MageBackend.Web.Middleware;

namespace MageBackend.Tests
{
    public class ErrorHandlerMiddlewareTests
    {
        private static async Task<(int StatusCode, string Body)> RunWithException(Exception ex)
        {
            var context = new DefaultHttpContext();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"err_{Guid.NewGuid()}")
                .Options;
            var services = new ServiceCollection();
            services.AddSingleton(new ApplicationDbContext(options));
            context.RequestServices = services.BuildServiceProvider();
            context.Response.Body = new MemoryStream();

            var middleware = new ErrorHandlerMiddleware(_ => throw ex);
            await middleware.InvokeAsync(context);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
            return (context.Response.StatusCode, body);
        }

        [Fact]
        public async Task NoException_ReturnsEmptyBody()
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            var middleware = new ErrorHandlerMiddleware(_ => Task.CompletedTask);
            await middleware.InvokeAsync(context);
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
            Assert.Equal("", body);
            Assert.Equal(200, context.Response.StatusCode);
        }

        [Fact]
        public async Task AppException_ReturnsCorrectStatusCode()
        {
            var (status, body) = await RunWithException(new AppException("Not found", 404));
            Assert.Equal(404, status);
        }

        [Fact]
        public async Task AppException_With401_ReturnsUnauthorizedFormat()
        {
            var (status, body) = await RunWithException(new AppException("Unauthorized", 401));
            Assert.Equal(401, status);
            using var json = JsonDocument.Parse(body);
            Assert.Equal("UnauthorizedError", json.RootElement.GetProperty("error").GetString());
        }

        [Fact]
        public async Task ValidationException_Returns400()
        {
            var failures = new[] { new FluentValidation.Results.ValidationFailure("Email", "Required") };
            var (status, body) = await RunWithException(new ValidationException(failures));
            Assert.Equal(400, status);
            using var json = JsonDocument.Parse(body);
            Assert.True(json.RootElement.TryGetProperty("errors", out _));
        }

        [Fact]
        public async Task GenericException_Returns500()
        {
            var (status, body) = await RunWithException(new InvalidOperationException("Boom"));
            Assert.Equal(500, status);
        }

        [Fact]
        public async Task ExceptionWithAuthenticatedUser_LogsError()
        {
            var context = new DefaultHttpContext();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"err_log_{Guid.NewGuid()}")
                .Options;
            var db = new ApplicationDbContext(options);
            var services = new ServiceCollection();
            services.AddSingleton(db);
            context.RequestServices = services.BuildServiceProvider();
            context.Response.Body = new MemoryStream();

            var identity = new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim("id", "user-1")
            }, "test");
            context.User = new System.Security.Claims.ClaimsPrincipal(identity);

            var middleware = new ErrorHandlerMiddleware(_ => throw new InvalidOperationException("DB Error"));
            await middleware.InvokeAsync(context);

            Assert.Equal(1, await db.ErrorLog.CountAsync());
        }

        [Fact]
        public async Task ExceptionWithoutUser_DoesNotLogError()
        {
            var context = new DefaultHttpContext();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"err_nolog_{Guid.NewGuid()}")
                .Options;
            var db = new ApplicationDbContext(options);
            var services = new ServiceCollection();
            services.AddSingleton(db);
            context.RequestServices = services.BuildServiceProvider();
            context.Response.Body = new MemoryStream();

            var middleware = new ErrorHandlerMiddleware(_ => throw new AppException("Bad", 400));
            await middleware.InvokeAsync(context);

            Assert.Equal(0, await db.ErrorLog.CountAsync());
        }
    }
}
