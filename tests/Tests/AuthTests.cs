using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using MageBackend.Features.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;

namespace MageBackend.Tests.Tests
{
    public class AuthEndpointTests : IClassFixture<IntegrationTestFixture>
    {
        private readonly HttpClient _client;

        public AuthEndpointTests(IntegrationTestFixture fixture)
        {
            _client = fixture.CreateClient();
        }

        [Fact]
        public async Task Health_ShouldReturn200()
        {
            var response = await _client.GetAsync("/health");
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        }

        [Fact]
        public async Task Liveness_ShouldReturn200()
        {
            var response = await _client.GetAsync("/liveness");
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        }

        [Fact]
        public async Task Ready_ShouldReturn200()
        {
            var response = await _client.GetAsync("/ready");
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        }

        [Fact]
        public async Task Login_WithEmptyBody_ShouldReturn400()
        {
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/v1/auth/login", content);
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Jwks_ShouldReturnEmptyKeys()
        {
            var response = await _client.GetAsync("/v1/auth/.well-known/jwks.json");
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

            var body = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(body);
            json.RootElement.TryGetProperty("keys", out var keys).Should().BeTrue();
            keys.GetArrayLength().Should().Be(0);
        }
    }
}
