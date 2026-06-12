using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Xunit;
using MageBackend.Infrastructure.Auth;

namespace MageBackend.Tests
{
    public class JwtProviderTests
    {
        private readonly JwtProvider _provider = new("86941813-8b97-4cad-b0b2-f97734a947d7");

        private readonly AuthPayload _payload = new()
        {
            Id = "user-1",
            Email = "test@example.com",
            RoleId = "admin",
            Permissions = new List<PermissionClaim>
            {
                new() { Feature = "dashboard", Create = true, View = true, Delete = true, Activate = true }
            },
            SessionVersion = 3
        };

        [Fact]
        public void GenerateToken_ShouldReturnValidJwt()
        {
            var token = _provider.GenerateToken(_payload, TimeSpan.FromHours(1));
            Assert.NotNull(token);
            Assert.Equal(3, token.Split('.').Length);

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            Assert.Equal("user-1", jwt.Claims.First(c => c.Type == "id").Value);
            Assert.Equal("test@example.com", jwt.Claims.First(c => c.Type == "email").Value);
            Assert.Equal("3", jwt.Claims.First(c => c.Type == "sv").Value);
            Assert.NotNull(jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti));
        }

        [Fact]
        public void GenerateTokenPair_ShouldReturnBothTokens()
        {
            var pair = _provider.GenerateTokenPair(_payload);
            Assert.NotNull(pair.Token);
            Assert.NotNull(pair.RefreshToken);
            Assert.NotEqual(pair.Token, pair.RefreshToken);
        }

        [Fact]
        public void VerifyToken_ShouldDecodeValidToken()
        {
            var token = _provider.GenerateToken(_payload, TimeSpan.FromHours(1));
            var result = _provider.VerifyToken(token);

            Assert.NotNull(result);
            Assert.Equal("user-1", result.Id);
            Assert.Equal("test@example.com", result.Email);
            Assert.Equal("admin", result.RoleId);
            Assert.Single(result.Permissions);
            Assert.Equal("dashboard", result.Permissions[0].Feature);
            Assert.Equal(3, result.SessionVersion);
        }

        [Fact]
        public void VerifyToken_ShouldThrowOnInvalidToken()
        {
            Assert.Throws<UnauthorizedAccessException>(() => _provider.VerifyToken("invalid-token"));
        }

        [Fact]
        public void VerifyToken_ShouldThrowOnWrongSecret()
        {
            var otherProvider = new JwtProvider("different-secret-key-that-is-not-the-same");
            var token = otherProvider.GenerateToken(_payload, TimeSpan.FromHours(1));

            Assert.Throws<UnauthorizedAccessException>(() => _provider.VerifyToken(token));
        }

        [Fact]
        public void VerifyToken_ShouldHandleExpiredToken()
        {
            var token = _provider.GenerateToken(_payload, TimeSpan.FromMilliseconds(1));
            Thread.Sleep(50);
            Assert.Throws<UnauthorizedAccessException>(() => _provider.VerifyToken(token));
        }
    }
}
