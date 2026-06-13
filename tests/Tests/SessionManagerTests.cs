using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using StackExchange.Redis;
using Xunit;
using Microsoft.EntityFrameworkCore;
using MageBackend.Database;
using MageBackend.Infrastructure.Auth;

namespace MageBackend.Tests
{
    public class SessionManagerTests
    {
        private readonly Mock<IDatabase> _redisMock = new();

        [Fact]
        public async Task GetCurrentVersionAsync_WhenRedisHasValue_ReturnsCachedVersion()
        {
            _redisMock.Setup(r => r.StringGetAsync("session:user:u1:version", It.IsAny<CommandFlags>()))
                .ReturnsAsync(new RedisValue("5"));

            var result = await SessionManager.GetCurrentVersionAsync("u1", null!, _redisMock.Object);

            Assert.Equal(5, result);
        }

        [Fact]
        public async Task GetCurrentVersionAsync_WhenRedisMiss_HydratesFromDatabase()
        {
            _redisMock.Setup(r => r.StringGetAsync("session:user:u1:version", It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisValue.Null);

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"test_{Guid.NewGuid()}")
                .Options;
            using var ctx = new ApplicationDbContext(options);

            var auth = new Auth { Id = "a1", Password = "hash", Active = true, SessionVersion = 7 };
            ctx.Auth.Add(auth);
            ctx.User.Add(new User
            {
                Id = "u1",
                Name = "Test",
                Email = "test@test.com",
                IdRole = "admin",
                Active = true,
                IdAuth = "a1"
            });
            await ctx.SaveChangesAsync();

            var result = await SessionManager.GetCurrentVersionAsync("u1", ctx, _redisMock.Object);

            Assert.Equal(7, result);
        }

        [Fact]
        public async Task GetCurrentVersionAsync_WhenNoAuth_ReturnsNull()
        {
            _redisMock.Setup(r => r.StringGetAsync("session:user:u2:version", It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisValue.Null);

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"test_{Guid.NewGuid()}")
                .Options;
            using var ctx = new ApplicationDbContext(options);

            ctx.User.Add(new User { Id = "u2", Name = "No Auth", Email = "noauth@test.com", IdRole = "admin", Active = true });
            await ctx.SaveChangesAsync();

            var result = await SessionManager.GetCurrentVersionAsync("u2", ctx, _redisMock.Object);

            Assert.Null(result);
        }

        [Fact]
        public async Task InvalidateUserSessionsAsync_WhenUserHasNoAuth_ReturnsZero()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"test_{Guid.NewGuid()}")
                .Options;
            using var ctx = new ApplicationDbContext(options);

            ctx.User.Add(new User { Id = "u3", Name = "No Auth", Email = "noauth2@test.com", IdRole = "admin", Active = true });
            await ctx.SaveChangesAsync();

            var result = await SessionManager.InvalidateUserSessionsAsync("u3", ctx);

            Assert.Equal(0, result);
        }

        [Fact]
        public async Task InvalidateManyUsersSessionsAsync_WithEmptyList_DoesNothing()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"test_{Guid.NewGuid()}")
                .Options;
            using var ctx = new ApplicationDbContext(options);

            await SessionManager.InvalidateManyUsersSessionsAsync(new List<string>(), ctx);
            Assert.Empty(await ctx.User.ToListAsync());
        }
    }
}
