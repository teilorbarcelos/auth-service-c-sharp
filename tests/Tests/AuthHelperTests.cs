using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using MageBackend.Database;
using MageBackend.Features.Auth;
using MageBackend.Infrastructure.Auth;

namespace MageBackend.Tests
{
    public class AuthHelperTests
    {
        private readonly JwtProvider _jwtProvider = new("86941813-8b97-4cad-b0b2-f97734a947d7");

        public AuthHelperTests()
        {
            RedisProvider.Initialize("redis://localhost:6379");
        }

        [Fact]
        public async Task GenerateAuthResponse_WithRoleName_ReturnsCorrectRoleName()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"auth_test_{Guid.NewGuid()}")
                .Options;
            using var ctx = new ApplicationDbContext(options);

            var role = new Role { Id = "admin", Name = "Administrador", Description = "Full Access", Active = true };
            ctx.Role.Add(role);
            ctx.RoleFeature.Add(new RoleFeature { IdRole = "admin", IdFeature = "dashboard", Create = true, View = true, Activate = true, Delete = true });
            ctx.RoleFeature.Add(new RoleFeature { IdRole = "admin", IdFeature = "user", Create = true, View = true, Activate = true, Delete = true });
            var auth = new Auth { Id = "a1", Password = "hash", Active = true, SessionVersion = 3 };
            ctx.Auth.Add(auth);
            ctx.User.Add(new User
            {
                Id = "u1", Name = "Test User", Email = "test@test.com", IdRole = "admin",
                Active = true, IdAuth = "a1"
            });
            await ctx.SaveChangesAsync();

            var userEntity = await ctx.User
                .Include(u => u.Role)
                .Include(u => u.Auth)
                .FirstAsync(u => u.Id == "u1");

            var result = await AuthHelper.GenerateAuthResponse(userEntity, ctx, _jwtProvider);

            Assert.NotNull(result);
            Assert.Equal("Test User", result.User.Name);
            Assert.Equal("admin", result.User.Role.Id);
            Assert.Equal("Administrador", result.User.Role.Name);
            Assert.Equal(2, result.User.Role.Permissions.Count);
        }

        [Fact]
        public async Task GenerateAuthResponse_WithoutRole_ReturnsEmptyRoleName()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"auth_test_{Guid.NewGuid()}")
                .Options;
            using var ctx = new ApplicationDbContext(options);

            ctx.User.Add(new User
            {
                Id = "u2", Name = "No Role", Email = "norole@test.com",
                IdRole = "nonexistent", Active = true
            });
            await ctx.SaveChangesAsync();

            var userEntity = await ctx.User.FirstAsync(u => u.Id == "u2");

            var result = await AuthHelper.GenerateAuthResponse(userEntity, ctx, _jwtProvider);

            Assert.NotNull(result);
            Assert.Empty(result.User.Role.Permissions);
        }

        [Fact]
        public async Task GenerateAuthResponse_WithoutAuth_DefaultsSessionVersionToOne()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"auth_test_{Guid.NewGuid()}")
                .Options;
            using var ctx = new ApplicationDbContext(options);

            ctx.User.Add(new User
            {
                Id = "u3", Name = "No Auth", Email = "noauth@test.com",
                IdRole = "admin", Active = true
            });
            await ctx.SaveChangesAsync();

            var userEntity = await ctx.User.FirstAsync(u => u.Id == "u3");

            var result = await AuthHelper.GenerateAuthResponse(userEntity, ctx, _jwtProvider);

            Assert.NotNull(result);
            Assert.NotEmpty(result.Token);
            Assert.NotEmpty(result.RefreshToken);
        }
    }
}
