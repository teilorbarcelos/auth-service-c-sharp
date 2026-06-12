using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using MageBackend.Database;
using MageBackend.Features.Auth.Commands;
using MageBackend.Infrastructure.Auth;

namespace MageBackend.Tests
{
    public class AuthCommandHandlerTests
    {
        private readonly JwtProvider _jwtProvider = new("86941813-8b97-4cad-b0b2-f97734a947d7");

        public AuthCommandHandlerTests()
        {
            RedisProvider.Initialize("redis://localhost:6379");
        }

        private ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"auth_{Guid.NewGuid()}")
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task LoginHandler_UserNotFound_ReturnsUnauthorized()
        {
            using var ctx = CreateContext();
            var handler = new LoginHandler(ctx, _jwtProvider);

            var result = await handler.Handle(new LoginCommand("unknown@test.com", "pass"), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(401, result.StatusCode);
        }

        [Fact]
        public async Task LoginHandler_UserInactive_ReturnsUnauthorized()
        {
            using var ctx = CreateContext();
            var auth = new Auth { Id = "a1", Password = BCrypt.Net.BCrypt.HashPassword("pass", 12), Active = true };
            ctx.Auth.Add(auth);
            ctx.User.Add(new User { Id = "u1", Name = "Test", Email = "test@test.com", IdRole = "admin", Active = false, IdAuth = "a1" });
            ctx.Role.Add(new Role { Id = "admin", Name = "Admin", Active = true });
            await ctx.SaveChangesAsync();

            var handler = new LoginHandler(ctx, _jwtProvider);
            var result = await handler.Handle(new LoginCommand("test@test.com", "pass"), CancellationToken.None);
            Assert.False(result.Success);
        }

        [Fact]
        public async Task LoginHandler_RoleInactive_ReturnsUnauthorized()
        {
            using var ctx = CreateContext();
            var auth = new Auth { Id = "a2", Password = BCrypt.Net.BCrypt.HashPassword("pass", 12), Active = true };
            ctx.Auth.Add(auth);
            ctx.User.Add(new User { Id = "u2", Name = "Test", Email = "test2@test.com", IdRole = "admin", Active = true, IdAuth = "a2" });
            ctx.Role.Add(new Role { Id = "admin", Name = "Admin", Active = false });
            await ctx.SaveChangesAsync();

            var handler = new LoginHandler(ctx, _jwtProvider);
            var result = await handler.Handle(new LoginCommand("test2@test.com", "pass"), CancellationToken.None);
            Assert.False(result.Success);
        }

        [Fact]
        public async Task LoginHandler_WrongPassword_ReturnsUnauthorized()
        {
            using var ctx = CreateContext();
            var auth = new Auth { Id = "a3", Password = BCrypt.Net.BCrypt.HashPassword("correct", 12), Active = true };
            ctx.Auth.Add(auth);
            ctx.User.Add(new User { Id = "u3", Name = "Test", Email = "test3@test.com", IdRole = "admin", Active = true, IdAuth = "a3" });
            ctx.Role.Add(new Role { Id = "admin", Name = "Admin", Active = true });
            await ctx.SaveChangesAsync();

            var handler = new LoginHandler(ctx, _jwtProvider);
            var result = await handler.Handle(new LoginCommand("test3@test.com", "wrong"), CancellationToken.None);
            Assert.False(result.Success);
        }

        [Fact]
        public async Task LoginHandler_Success_ReturnsToken()
        {
            using var ctx = CreateContext();
            var auth = new Auth { Id = "a4", Password = BCrypt.Net.BCrypt.HashPassword("pass123", 12), Active = true, SessionVersion = 1 };
            ctx.Auth.Add(auth);
            ctx.User.Add(new User { Id = "u4", Name = "Test", Email = "test4@test.com", IdRole = "admin", Active = true, IdAuth = "a4" });
            ctx.Role.Add(new Role { Id = "admin", Name = "Admin", Active = true });
            await ctx.SaveChangesAsync();

            var handler = new LoginHandler(ctx, _jwtProvider);
            var result = await handler.Handle(new LoginCommand("test4@test.com", "pass123"), CancellationToken.None);
            Assert.True(result.Success);
            Assert.NotNull(result.Response);
            Assert.NotEmpty(result.Response.Token);
        }

        [Fact]
        public async Task LoginHandler_DeletedUser_ReturnsUnauthorized()
        {
            using var ctx = CreateContext();
            var auth = new Auth { Id = "a5", Password = "hash", Active = true, IsDeleted = true };
            ctx.Auth.Add(auth);
            ctx.User.Add(new User { Id = "u5", Name = "Test", Email = "test5@test.com", IdRole = "admin", Active = true, IdAuth = "a5" });
            ctx.Role.Add(new Role { Id = "admin", Name = "Admin", Active = true });
            await ctx.SaveChangesAsync();

            var handler = new LoginHandler(ctx, _jwtProvider);
            var result = await handler.Handle(new LoginCommand("test5@test.com", "pass"), CancellationToken.None);
            Assert.False(result.Success);
        }

        [Fact]
        public async Task LoginHandler_DeletedUserFlag_ReturnsUnauthorized()
        {
            using var ctx = CreateContext();
            ctx.User.Add(new User { Id = "u6", Name = "Test", Email = "test6@test.com", IdRole = "admin", Active = true, IsDeleted = true });
            ctx.Role.Add(new Role { Id = "admin", Name = "Admin", Active = true });
            await ctx.SaveChangesAsync();

            var handler = new LoginHandler(ctx, _jwtProvider);
            var result = await handler.Handle(new LoginCommand("test6@test.com", "pass"), CancellationToken.None);
            Assert.False(result.Success);
        }
    }
}
