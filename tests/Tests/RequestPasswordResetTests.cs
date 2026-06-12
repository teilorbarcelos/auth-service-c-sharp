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
    public class RequestPasswordResetTests
    {
        public RequestPasswordResetTests()
        {
            RedisProvider.Initialize("redis://localhost:6379");
        }

        [Fact]
        public async Task Handle_UserNotFound_ReturnsEmptyToken()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"rpr_{Guid.NewGuid()}")
                .Options;
            using var ctx = new ApplicationDbContext(options);

            var handler = new RequestPasswordResetHandler(ctx);
            var result = await handler.Handle(new RequestPasswordResetCommand("none@test.com"), CancellationToken.None);

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public async Task Handle_UserFound_SetsTokenAndExpiration()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"rpr_{Guid.NewGuid()}")
                .Options;
            using var ctx = new ApplicationDbContext(options);

            var auth = new Auth { Id = "a1", Password = "hash", Active = true };
            ctx.Auth.Add(auth);
            ctx.User.Add(new User { Id = "u1", Name = "T", Email = "t@t.com", IdRole = "admin", Active = true, IdAuth = "a1" });
            await ctx.SaveChangesAsync();

            var handler = new RequestPasswordResetHandler(ctx);
            var result = await handler.Handle(new RequestPasswordResetCommand("t@t.com"), CancellationToken.None);

            Assert.NotEqual(string.Empty, result);

            var updated = await ctx.Auth.FindAsync("a1");
            Assert.NotNull(updated!.RequestPasswordToken);
            Assert.NotNull(updated.RequestPasswordExpiration);
        }
    }
}
