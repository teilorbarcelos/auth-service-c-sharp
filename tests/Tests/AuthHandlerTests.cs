using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using MageBackend.Database;
using MageBackend.Features.Auth.Commands;
using MageBackend.Features.Auth.Queries;
using MageBackend.Infrastructure.Auth;

namespace MageBackend.Tests
{
    public class AuthHandlerTests
    {
        private readonly JwtProvider _jwtProvider = new("86941813-8b97-4cad-b0b2-f97734a947d7");

        public AuthHandlerTests()
        {
            RedisProvider.Initialize("redis://localhost:6379");
        }

        private ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"h_{Guid.NewGuid()}")
                .Options;
            return new ApplicationDbContext(options);
        }

        // ===== Logout =====

        [Fact]
        public async Task LogoutHandler_WithNullUserId_DoesNothing()
        {
            using var ctx = CreateContext();
            var handler = new LogoutHandler(ctx);
            await handler.Handle(new LogoutCommand(null), CancellationToken.None);
        }

        // ===== ChangePassword =====

        [Fact]
        public async Task ChangePasswordHandler_UserNotFound_ReturnsError()
        {
            using var ctx = CreateContext();
            var handler = new ChangePasswordHandler(ctx);
            var result = await handler.Handle(new ChangePasswordCommand("none@test.com", "token", "NewPass1!"), CancellationToken.None);
            Assert.False(result.Success);
            Assert.Equal(401, result.StatusCode);
        }

        [Fact]
        public async Task ChangePasswordHandler_TokenMismatch_ReturnsError()
        {
            using var ctx = CreateContext();
            var auth = new Auth { Id = "a1", Password = "hash", Active = true, RequestPasswordToken = "correct-token" };
            ctx.Auth.Add(auth);
            ctx.User.Add(new User { Id = "u1", Name = "T", Email = "t@t.com", IdRole = "admin", Active = true, IdAuth = "a1" });
            await ctx.SaveChangesAsync();

            var handler = new ChangePasswordHandler(ctx);
            var result = await handler.Handle(new ChangePasswordCommand("t@t.com", "wrong-token", "NewPass1!"), CancellationToken.None);
            Assert.False(result.Success);
        }

        [Fact]
        public async Task ChangePasswordHandler_TokenExpired_ReturnsError()
        {
            using var ctx = CreateContext();
            var auth = new Auth { Id = "a1", Password = "hash", Active = true, RequestPasswordToken = "expired", RequestPasswordExpiration = DateTime.UtcNow.AddHours(-1) };
            ctx.Auth.Add(auth);
            ctx.User.Add(new User { Id = "u1", Name = "T", Email = "t@t.com", IdRole = "admin", Active = true, IdAuth = "a1" });
            await ctx.SaveChangesAsync();

            var handler = new ChangePasswordHandler(ctx);
            var result = await handler.Handle(new ChangePasswordCommand("t@t.com", "expired", "NewPass1!"), CancellationToken.None);
            Assert.False(result.Success);
        }

        [Fact(Skip = "Requires real SQL Server (ExecuteUpdateAsync not supported by InMemory)")]
        public async Task ChangePasswordHandler_Success_UpdatesPassword()
        {
        }

        // ===== ValidateResetToken =====

        [Fact]
        public async Task ValidateResetTokenHandler_UserNotFound_ReturnsError()
        {
            using var ctx = CreateContext();
            var handler = new ValidateResetTokenHandler(ctx);
            var result = await handler.Handle(new ValidateResetTokenCommand("none@test.com", "token"), CancellationToken.None);
            Assert.False(result.Success);
        }

        [Fact]
        public async Task ValidateResetTokenHandler_TokenMismatch_ReturnsError()
        {
            using var ctx = CreateContext();
            ctx.Auth.Add(new Auth { Id = "a1", Password = "hash", Active = true, RequestPasswordToken = "real-token" });
            ctx.User.Add(new User { Id = "u1", Name = "T", Email = "t@t.com", IdRole = "admin", Active = true, IdAuth = "a1" });
            await ctx.SaveChangesAsync();

            var handler = new ValidateResetTokenHandler(ctx);
            var result = await handler.Handle(new ValidateResetTokenCommand("t@t.com", "fake-token"), CancellationToken.None);
            Assert.False(result.Success);
        }

        [Fact]
        public async Task ValidateResetTokenHandler_Success_ReturnsValid()
        {
            using var ctx = CreateContext();
            ctx.Auth.Add(new Auth { Id = "a1", Password = "hash", Active = true, RequestPasswordToken = "good-token", RequestPasswordExpiration = DateTime.UtcNow.AddHours(1) });
            ctx.User.Add(new User { Id = "u1", Name = "T", Email = "t@t.com", IdRole = "admin", Active = true, IdAuth = "a1" });
            await ctx.SaveChangesAsync();

            var handler = new ValidateResetTokenHandler(ctx);
            var result = await handler.Handle(new ValidateResetTokenCommand("t@t.com", "good-token"), CancellationToken.None);
            Assert.True(result.Success);
        }

        // ===== GetMe =====

        [Fact]
        public async Task GetMeHandler_NullUserId_ReturnsUnauthorized()
        {
            using var ctx = CreateContext();
            var handler = new GetMeHandler(ctx);
            var result = await handler.Handle(new GetMeQuery(null, null), CancellationToken.None);
            Assert.False(result.Success);
            Assert.Equal(401, result.StatusCode);
        }

        [Fact]
        public async Task GetMeHandler_UserNotFound_ReturnsUnauthorized()
        {
            using var ctx = CreateContext();
            var handler = new GetMeHandler(ctx);
            var result = await handler.Handle(new GetMeQuery("nonexistent", null), CancellationToken.None);
            Assert.False(result.Success);
        }

        [Fact]
        public async Task GetMeHandler_Success_ReturnsUserData()
        {
            using var ctx = CreateContext();
            ctx.Role.Add(new Role { Id = "admin", Name = "Admin", Active = true });
            ctx.Auth.Add(new Auth { Id = "a1", Password = "hash", Active = true });
            ctx.User.Add(new User { Id = "u1", Name = "Test User", Email = "t@t.com", IdRole = "admin", Active = true, IdAuth = "a1" });
            await ctx.SaveChangesAsync();

            var handler = new GetMeHandler(ctx);
            var result = await handler.Handle(new GetMeQuery("u1", "Bearer test-token"), CancellationToken.None);
            Assert.True(result.Success);
            Assert.Equal("Test User", result.Response!.User.Name);
            Assert.Equal("test-token", result.Response.Token);
        }

        [Fact]
        public async Task GetMeHandler_UserInactive_ReturnsUnauthorized()
        {
            using var ctx = CreateContext();
            ctx.Auth.Add(new Auth { Id = "a1", Password = "hash", Active = true });
            ctx.User.Add(new User { Id = "u1", Name = "T", Email = "t@t.com", IdRole = "admin", Active = false, IdAuth = "a1" });
            await ctx.SaveChangesAsync();

            var handler = new GetMeHandler(ctx);
            var result = await handler.Handle(new GetMeQuery("u1", null), CancellationToken.None);
            Assert.False(result.Success);
        }
    }
}
