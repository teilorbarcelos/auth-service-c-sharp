using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using MageBackend.Database;
using MageBackend.Infrastructure.Auth;
using MageBackend.Web.Middleware;

namespace MageBackend.Tests
{
    public class TokenSessionValidationMiddlewareTests
    {
        public TokenSessionValidationMiddlewareTests()
        {
            RedisProvider.Initialize("redis://localhost:6379");
        }

        [Fact]
        public async Task PublicPath_Login_SkipsValidation()
        {
            var context = MakeContext("/v1/auth/login");
            var middleware = new TokenSessionValidationMiddleware(_ => Task.CompletedTask);
            await middleware.InvokeAsync(context);
            Assert.Equal(200, context.Response.StatusCode);
        }

        [Fact]
        public async Task PublicPath_Health_SkipsValidation()
        {
            var context = MakeContext("/health");
            var middleware = new TokenSessionValidationMiddleware(_ => Task.CompletedTask);
            await middleware.InvokeAsync(context);
            Assert.Equal(200, context.Response.StatusCode);
        }

        [Fact]
        public async Task PublicPath_PasswordReset_SkipsValidation()
        {
            var context = MakeContext("/v1/auth/password/request");
            var middleware = new TokenSessionValidationMiddleware(_ => Task.CompletedTask);
            await middleware.InvokeAsync(context);
            Assert.Equal(200, context.Response.StatusCode);
        }

        [Fact]
        public async Task UnauthenticatedRequest_PassesThrough()
        {
            var context = MakeContext("/v1/user");
            var middleware = new TokenSessionValidationMiddleware(_ => Task.CompletedTask);
            await middleware.InvokeAsync(context);
            Assert.Equal(200, context.Response.StatusCode);
        }

        [Fact]
        public async Task AuthenticatedRequest_WithValidSession_PassesThrough()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"tsv_{Guid.NewGuid()}")
                .Options;
            var ctx = new ApplicationDbContext(options);
            var auth = new Auth { Id = "a1", Password = "hash", Active = true, SessionVersion = 1 };
            ctx.Auth.Add(auth);
            ctx.User.Add(new User { Id = "u1", Name = "T", Email = "t@t.com", IdRole = "admin", Active = true, IdAuth = "a1" });
            ctx.SaveChanges();

            var redisDb = RedisProvider.Database;
            await redisDb.StringSetAsync($"session:user:u1:version", "1", TimeSpan.FromDays(7));

            var context = MakeContext("/v1/user", withUser: true, dbContext: ctx);
            var middleware = new TokenSessionValidationMiddleware(_ => Task.CompletedTask);
            await middleware.InvokeAsync(context);
            Assert.Equal(200, context.Response.StatusCode);

            ctx.Dispose();
        }

        private static DefaultHttpContext MakeContext(string path, bool withUser = false, ApplicationDbContext? dbContext = null)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = path;
            context.Response.Body = new MemoryStream();

            if (withUser)
            {
                var identity = new ClaimsIdentity(new[]
                {
                    new Claim("id", "u1"),
                    new Claim("sv", "1")
                }, "jwt");
                context.User = new ClaimsPrincipal(identity);
            }
            else
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity());
            }

            if (dbContext != null)
            {
                var services = new ServiceCollection();
                services.AddSingleton(dbContext);
                context.RequestServices = services.BuildServiceProvider();
            }

            return context;
        }
    }
}
