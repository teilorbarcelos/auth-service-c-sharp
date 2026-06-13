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
    public class RefreshTokenHandlerTests
    {
        private readonly JwtProvider _jwtProvider = new("86941813-8b97-4cad-b0b2-f97734a947d7");

        public RefreshTokenHandlerTests()
        {
            RedisProvider.Initialize("redis://localhost:6379");
        }

        [Fact]
        public async Task Handle_InvalidToken_ReturnsUnauthorized()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"rt_{Guid.NewGuid()}")
                .Options;
            using var ctx = new ApplicationDbContext(options);

            var handler = new RefreshTokenHandler(ctx, _jwtProvider);
            var result = await handler.Handle(new RefreshTokenCommand("invalid-token"), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(401, result.StatusCode);
        }

        [Fact]
        public async Task Handle_ValidTokenNoRedisKey_ReturnsUnauthorized()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"rt_{Guid.NewGuid()}")
                .Options;
            using var ctx = new ApplicationDbContext(options);

            var payload = new AuthPayload { Id = "u1", Email = "t@t.com", RoleId = "admin", SessionVersion = 1 };
            var token = _jwtProvider.GenerateToken(payload, TimeSpan.FromDays(7));

            var handler = new RefreshTokenHandler(ctx, _jwtProvider);
            var result = await handler.Handle(new RefreshTokenCommand(token), CancellationToken.None);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task Handle_UserNotFound_ReturnsUnauthorized()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"rt_{Guid.NewGuid()}")
                .Options;
            using var ctx = new ApplicationDbContext(options);

            var payload = new AuthPayload { Id = "nonexistent", Email = "t@t.com", RoleId = "admin", SessionVersion = 1 };
            var token = _jwtProvider.GenerateToken(payload, TimeSpan.FromDays(7));

            // Set the Redis key to pass first check
            var tokenHash = Convert.ToHexStringLower(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(token)));
            var redis = RedisProvider.Database;
            await redis.StringSetAsync($"session:user:nonexistent:refresh:{tokenHash}", "1", TimeSpan.FromDays(7));
            await redis.StringSetAsync("session:user:nonexistent:version", "1", TimeSpan.FromDays(7));

            var handler = new RefreshTokenHandler(ctx, _jwtProvider);
            var result = await handler.Handle(new RefreshTokenCommand(token), CancellationToken.None);

            Assert.False(result.Success);
        }

#pragma warning disable xUnit1004
        [Fact(Skip = "Requires real SQL Server (ExecuteUpdateAsync in InvalidateUserSessionsAsync)")]
        public async Task Handle_FullSuccessPath() { }
#pragma warning restore xUnit1004
    }
}
