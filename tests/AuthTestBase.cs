using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace MageBackend.Tests
{
    public abstract class AuthTestBase : IClassFixture<IntegrationTestFixture>
    {
        protected readonly IntegrationTestFixture _fixture;
        protected readonly HttpClient _client;

        protected AuthTestBase(IntegrationTestFixture fixture)
        {
            _fixture = fixture;
            _client = _fixture.CreateClient();
        }

        protected async Task<LoginResponse> LoginAsync(string email, string password)
        {
            var response = await _client.PostAsJsonAsync("/v1/auth/login", new { email, password });
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.NotNull(data);
            return data;
        }

        protected void SetAuthHeader(string token)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        protected void ClearAuthHeader()
        {
            _client.DefaultRequestHeaders.Authorization = null;
        }
    }

    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public LoginUserResponse User { get; set; } = new();
    }

    public class LoginUserResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public LoginUserRoleResponse Role { get; set; } = new();
    }

    public class LoginUserRoleResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
