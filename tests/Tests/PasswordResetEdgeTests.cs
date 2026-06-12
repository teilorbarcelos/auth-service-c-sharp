using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using MageBackend.Database;
using MageBackend.Features.Auth.Commands;

namespace MageBackend.Tests
{
    public class ChangePasswordTests
    {
        [Fact]
        public async Task Handle_UserNotFound_ReturnsError()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"cp_{Guid.NewGuid()}")
                .Options;
            using var ctx = new ApplicationDbContext(options);

            var handler = new ChangePasswordHandler(ctx);
            var result = await handler.Handle(new ChangePasswordCommand("none@t.com", "t", "P"), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(401, result.StatusCode);
        }

        [Fact]
        public async Task Handle_TokenExpired_ReturnsError()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"cp_{Guid.NewGuid()}")
                .Options;
            using var ctx = new ApplicationDbContext(options);

            var auth = new Auth { Id = "a1", Password = "hash", Active = true,
                RequestPasswordToken = "expired-token",
                RequestPasswordExpiration = DateTime.UtcNow.AddHours(-1) };
            ctx.Auth.Add(auth);
            ctx.User.Add(new User { Id = "u1", Name = "T", Email = "t@t.com", IdRole = "admin", Active = true, IdAuth = "a1" });
            await ctx.SaveChangesAsync();

            var handler = new ChangePasswordHandler(ctx);
            var result = await handler.Handle(new ChangePasswordCommand("t@t.com", "expired-token", "NewP1!"), CancellationToken.None);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task ValidateReset_TokenExpired_ReturnsError()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"vr_{Guid.NewGuid()}")
                .Options;
            using var ctx = new ApplicationDbContext(options);

            ctx.Auth.Add(new Auth { Id = "a1", Password = "hash", Active = true,
                RequestPasswordToken = "old-token",
                RequestPasswordExpiration = DateTime.UtcNow.AddHours(-2) });
            ctx.User.Add(new User { Id = "u1", Name = "T", Email = "t@t.com", IdRole = "admin", Active = true, IdAuth = "a1" });
            await ctx.SaveChangesAsync();

            var handler = new ValidateResetTokenHandler(ctx);
            var result = await handler.Handle(new ValidateResetTokenCommand("t@t.com", "old-token"), CancellationToken.None);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task ValidateReset_TokenMismatch_ReturnsError()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"vr_{Guid.NewGuid()}")
                .Options;
            using var ctx = new ApplicationDbContext(options);

            ctx.Auth.Add(new Auth { Id = "a1", Password = "hash", Active = true,
                RequestPasswordToken = "correct-token",
                RequestPasswordExpiration = DateTime.UtcNow.AddHours(1) });
            ctx.User.Add(new User { Id = "u1", Name = "T", Email = "t@t.com", IdRole = "admin", Active = true, IdAuth = "a1" });
            await ctx.SaveChangesAsync();

            var handler = new ValidateResetTokenHandler(ctx);
            var result = await handler.Handle(new ValidateResetTokenCommand("t@t.com", "wrong-token"), CancellationToken.None);

            Assert.False(result.Success);
        }
    }
}
