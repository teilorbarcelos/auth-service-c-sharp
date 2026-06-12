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
            var result = await RunMiddleware("/v1/auth/login");
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public async Task PublicPath_Health_SkipsValidation()
        {
            var result = await RunMiddleware("/health");
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public async Task PublicPath_PasswordReset_SkipsValidation()
        {
            var result = await RunMiddleware("/v1/auth/password/request");
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public async Task Authenticated_WithValidSession_PassesThrough()
        {
            var (db, context) = CreateDbContext();
            var auth = new Auth { Id = "a1", Password = "hash", Active = true, SessionVersion = 5 };
            db.Auth.Add(auth);
            db.User.Add(new User { Id = "u1", Name = "T", Email = "t@t.com", IdRole = "admin", Active = true, IdAuth = "a1" });
            await db.SaveChangesAsync();

            var redis = RedisProvider.Database;
            await redis.StringSetAsync("session:user:u1:version", "5", TimeSpan.FromDays(7));

            SetupUser(context, "u1", "5");
            var middleware = new TokenSessionValidationMiddleware(_ => Task.CompletedTask);
            await middleware.InvokeAsync(context);
            Assert.Equal(200, context.Response.StatusCode);
            db.Dispose();
        }

        [Fact]
        public async Task Authenticated_WithStaleSession_Rejects()
        {
            var (db, context) = CreateDbContext();
            var auth = new Auth { Id = "a1", Password = "hash", Active = true, SessionVersion = 5 };
            db.Auth.Add(auth);
            db.User.Add(new User { Id = "u1", Name = "T", Email = "t@t.com", IdRole = "admin", Active = true, IdAuth = "a1" });
            await db.SaveChangesAsync();

            var redis = RedisProvider.Database;
            await redis.StringSetAsync("session:user:u1:version", "10", TimeSpan.FromDays(7));

            SetupUser(context, "u1", "5");
            var middleware = new TokenSessionValidationMiddleware(_ => Task.CompletedTask);
            await middleware.InvokeAsync(context);

            Assert.NotEqual(200, context.Response.StatusCode);
            db.Dispose();
        }

        [Fact]
        public async Task Unauthenticated_PassesThrough()
        {
            var result = await RunMiddleware("/v1/user");
            Assert.Equal(200, result.StatusCode);
        }

        private static async Task<(int StatusCode, string? Body)> RunMiddleware(string path)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = path;
            context.Response.Body = new MemoryStream();
            context.User = new ClaimsPrincipal(new ClaimsIdentity());

            var middleware = new TokenSessionValidationMiddleware(_ => Task.CompletedTask);
            await middleware.InvokeAsync(context);

            return (context.Response.StatusCode, null);
        }

        private static (ApplicationDbContext db, DefaultHttpContext context) CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"tsm_{Guid.NewGuid()}")
                .Options;
            var db = new ApplicationDbContext(options);
            var services = new ServiceCollection();
            services.AddSingleton(db);

            var context = new DefaultHttpContext();
            context.Request.Path = "/v1/user";
            context.Response.Body = new MemoryStream();
            context.RequestServices = services.BuildServiceProvider();

            return (db, context);
        }

        private static void SetupUser(DefaultHttpContext context, string userId, string sv)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim("id", userId),
                new Claim("sv", sv)
            }, "jwt");
            context.User = new ClaimsPrincipal(identity);
        }
    }
}
