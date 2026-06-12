using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;
using MageBackend.Infrastructure.Auth;
using MageBackend.Web.Middleware;

namespace MageBackend.Tests
{
    public class JwtAuthMiddlewareTests
    {
        private readonly JwtProvider _jwtProvider = new("86941813-8b97-4cad-b0b2-f97734a947d7");

        [Fact]
        public async Task WithValidToken_SetsUserPrincipal()
        {
            var payload = new AuthPayload { Id = "u1", Email = "test@test.com", RoleId = "admin" };
            var token = _jwtProvider.GenerateToken(payload, TimeSpan.FromHours(1));

            var context = new DefaultHttpContext();
            context.Request.Headers.Authorization = $"Bearer {token}";
            context.Response.Body = new MemoryStream();

            var middleware = new JwtAuthenticationMiddleware(_ => Task.CompletedTask);
            await middleware.InvokeAsync(context, _jwtProvider);

            Assert.NotNull(context.User);
            Assert.True(context.User.Identity?.IsAuthenticated);
            Assert.Equal("u1", context.User.FindFirst("id")?.Value);
        }

        [Fact]
        public async Task WithInvalidToken_DoesNotSetUser()
        {
            var context = new DefaultHttpContext();
            context.Request.Headers.Authorization = "Bearer invalid-token";
            context.Response.Body = new MemoryStream();

            var middleware = new JwtAuthenticationMiddleware(_ => Task.CompletedTask);
            await middleware.InvokeAsync(context, _jwtProvider);

            Assert.False(context.User.Identity?.IsAuthenticated ?? true);
        }

        [Fact]
        public async Task WithNoToken_DoesNotSetUser()
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            var middleware = new JwtAuthenticationMiddleware(_ => Task.CompletedTask);
            await middleware.InvokeAsync(context, _jwtProvider);

            Assert.NotNull(context.User);
            Assert.False(context.User.Identity?.IsAuthenticated);
        }
    }
}
