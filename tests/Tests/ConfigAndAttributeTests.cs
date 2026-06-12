using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using MageBackend.Web.Filters;
using MageBackend.Web.Middleware;
using MageBackend.Infrastructure.Configuration;

namespace MageBackend.Tests
{
    public class CheckPermissionAttributeTests
    {
        [Fact]
        public void OnAuthorization_WithPermission_Allows()
        {
            var ctx = CreateContext(new[] { new Claim("permissions", "[{\"feature\":\"dashboard\",\"view\":true}]") });
            var attr = new CheckPermissionAttribute("dashboard", "view");
            attr.OnAuthorization(ctx);
            Assert.Null(ctx.Result);
        }

        [Fact]
        public void OnAuthorization_WithoutPermission_Throws()
        {
            var ctx = CreateContext(new[] { new Claim("permissions", "[{\"feature\":\"user\",\"view\":false}]") });
            var attr = new CheckPermissionAttribute("user", "view");
            Assert.Throws<AppException>(() => attr.OnAuthorization(ctx));
        }

        [Fact]
        public void OnAuthorization_WithoutUser_Throws()
        {
            var ctx = CreateContext(Array.Empty<Claim>());
            var attr = new CheckPermissionAttribute("any", "view");
            Assert.Throws<AppException>(() => attr.OnAuthorization(ctx));
        }

        [Fact]
        public void OnAuthorization_WithoutPermissionClaim_Throws()
        {
            var ctx = CreateContext(new[] { new Claim("email", "test@test.com") });
            var attr = new CheckPermissionAttribute("dashboard", "view");
            Assert.Throws<AppException>(() => attr.OnAuthorization(ctx));
        }

        [Fact]
        public void FeatureNameAttribute_StoresName()
        {
            var attr = new FeatureNameAttribute("test-feature");
            Assert.Equal("test-feature", attr.Name);
        }

        private static AuthorizationFilterContext CreateContext(Claim[] claims)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();
            if (claims.Length > 0)
            {
                httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
            }
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
        }
    }

    public class CorsConfigTests
    {
        [Fact]
        public void GetAllowedOrigins_Local_ReturnsDevDefaults()
        {
            var orig = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS");
            Environment.SetEnvironmentVariable("CORS_ALLOWED_ORIGINS", null);
            var origins = CorsConfig.GetAllowedOrigins("local");
            Assert.Contains("http://localhost:3000", origins);
            Environment.SetEnvironmentVariable("CORS_ALLOWED_ORIGINS", orig);
        }

        [Fact]
        public void GetAllowedOrigins_Production_ReturnsConfiguredOrigins()
        {
            var orig = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS");
            Environment.SetEnvironmentVariable("CORS_ALLOWED_ORIGINS", "https://app.com,https://admin.com");
            var origins = CorsConfig.GetAllowedOrigins("Production");
            Assert.Contains("https://app.com", origins);
            Assert.Contains("https://admin.com", origins);
            Environment.SetEnvironmentVariable("CORS_ALLOWED_ORIGINS", orig);
        }

        [Fact]
        public void GetAllowedOrigins_Production_WithoutEnv_Throws()
        {
            var orig = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS");
            Environment.SetEnvironmentVariable("CORS_ALLOWED_ORIGINS", null);
            Assert.Throws<InvalidOperationException>(() => CorsConfig.GetAllowedOrigins("Production"));
            Environment.SetEnvironmentVariable("CORS_ALLOWED_ORIGINS", orig);
        }

        [Fact]
        public void IsProduction_ReturnsTrueForProduction()
        {
            Assert.True(CorsConfig.IsProduction("Production"));
            Assert.False(CorsConfig.IsProduction("Development"));
        }
    }

    public class RateLimitConfigTests
    {
        [Fact]
        public void GetFor_KnownEndpoint_ReturnsSpecificLimit()
        {
            var limit = RateLimitConfig.GetFor("/v1/auth/login");
            Assert.Equal(5, limit.Max);
            Assert.Equal(60, limit.WindowSeconds);
        }

        [Fact]
        public void GetFor_NullPath_ReturnsDefault()
        {
            var limit = RateLimitConfig.GetFor(null);
            Assert.Equal(100, limit.Max);
        }

        [Fact]
        public void GetFor_UnknownPath_ReturnsDefault()
        {
            var limit = RateLimitConfig.GetFor("/unknown/path");
            Assert.Equal(100, limit.Max);
        }

        [Fact]
        public void IsExempt_KnownExemptPath_ReturnsTrue()
        {
            Assert.False(RateLimitConfig.IsExempt("/v1/auth/login"));
        }

        [Fact]
        public void IsExempt_Null_ReturnsFalse()
        {
            Assert.False(RateLimitConfig.IsExempt(null));
        }
    }
}
