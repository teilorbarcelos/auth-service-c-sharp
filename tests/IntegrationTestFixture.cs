using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.MsSql;
using Testcontainers.Redis;
using Xunit;

namespace MageBackend.Tests
{
    public class IntegrationTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("YourPassword123!")
            .Build();

        private readonly RedisContainer _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        public async Task InitializeAsync()
        {
            await _msSqlContainer.StartAsync();
            await _redisContainer.StartAsync();

            var connectionString = _msSqlContainer.GetConnectionString();

            Environment.SetEnvironmentVariable("DATABASE_URL", connectionString);
            Environment.SetEnvironmentVariable("REDIS_URL", _redisContainer.GetConnectionString());
            Environment.SetEnvironmentVariable("DISABLE_RATE_LIMIT", "true");
            Environment.SetEnvironmentVariable("JWT_SECRET", "86941813-8b97-4cad-b0b2-f97734a947d7");
        }

        public new async Task DisposeAsync()
        {
            await _msSqlContainer.DisposeAsync();
            await _redisContainer.DisposeAsync();
            await base.DisposeAsync();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
        }
    }
}
