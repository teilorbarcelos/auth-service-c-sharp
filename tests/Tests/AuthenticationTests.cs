using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MageBackend.Database;
using MageBackend.Infrastructure.Auth;
using Xunit;

namespace MageBackend.Tests.Tests
{
    public class AuthenticationTests : AuthTestBase
    {
        public AuthenticationTests(IntegrationTestFixture fixture) : base(fixture) { }

        [Fact]
        public async Task GivenInvalidCredentials_WhenLoggingIn_ThenReturnsUnauthorized()
        {
            var invalidResp = await _client.PostAsJsonAsync("/v1/auth/login", new { email = "wrong@example.com", password = "wrong" });
            Assert.Equal(HttpStatusCode.Unauthorized, invalidResp.StatusCode);
        }

        [Fact]
        public async Task GivenValidCredentials_WhenLoggingIn_ThenReturnsAuthTokens()
        {
            var loginData = await LoginAsync("admin@email.com", "admin@123");
            Assert.NotEmpty(loginData.Token);
            Assert.NotEmpty(loginData.RefreshToken);
            Assert.Equal("admin@email.com", loginData.User.Email);
        }

        [Fact]
        public async Task GivenValidLogin_WhenProcessed_ThenRedisSessionVersionIsCreated()
        {
            var loginData = await LoginAsync("admin@email.com", "admin@123");
            var db = RedisProvider.Database;

            var versionKey = $"session:user:{loginData.User.Id}:version";
            var version = await db.StringGetAsync(versionKey);
            Assert.True(version.HasValue);
        }

        [Fact]
        public async Task GivenValidToken_WhenAccessingMe_ThenReturnsUserData()
        {
            var loginData = await LoginAsync("admin@email.com", "admin@123");
            SetAuthHeader(loginData.Token);

            var meResponse = await _client.GetAsync("/v1/auth/me");
            Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        }

        [Fact]
        public async Task GivenNoToken_WhenAccessingMe_ThenReturnsError()
        {
            var meResponse = await _client.GetAsync("/v1/auth/me");
            Assert.True((int)meResponse.StatusCode >= 400, $"Expected error status, got {(int)meResponse.StatusCode}");
        }

        [Fact]
        public async Task GivenValidRefreshToken_WhenRefreshing_ThenReturnsNewTokens()
        {
            var loginData = await LoginAsync("admin@email.com", "admin@123");
            var refreshResponse = await _client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = loginData.RefreshToken });
            Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

            var refreshed = await refreshResponse.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.NotNull(refreshed);
            Assert.NotEmpty(refreshed.Token);
        }

        [Fact]
        public async Task GivenInvalidRefreshToken_WhenRefreshing_ThenReturnsUnauthorized()
        {
            var refreshResponse = await _client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = "invalid-refresh-token" });
            Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
        }

        [Fact]
        public async Task GivenValidToken_WhenLoggingOut_ThenSessionIsRevoked()
        {
            var loginData = await LoginAsync("admin@email.com", "admin@123");
            SetAuthHeader(loginData.Token);

            var logoutResponse = await _client.PostAsync("/v1/auth/logout", null);
            Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);
        }

        [Fact]
        public async Task GivenNoToken_WhenLoggingOut_ThenReturnsOk()
        {
            var logoutResponse = await _client.PostAsync("/v1/auth/logout", null);
            Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);
        }

        [Fact]
        public async Task GivenEmail_WhenRequestingPasswordReset_ThenReturnsOk()
        {
            var response = await _client.PostAsJsonAsync("/v1/auth/password/request", new { email = "admin@email.com" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
