using System;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Moq;
using Xunit;
using MageBackend.Features.Auth;
using MageBackend.Features.Auth.Commands;
using MageBackend.Features.Auth.Queries;
using MageBackend.Web;
using MageBackend.Web.Filters;
using MageBackend.Web.Middleware;

namespace MageBackend.Tests
{
    public class BaseApiControllerTests
    {
        [Fact]
        public void ErrorResponse_ReturnsCorrectStatusCode()
        {
            var result = BaseApiControllerHelper.GetErrorResponse("test error", 400) as ObjectResult;
            Assert.NotNull(result);
            Assert.Equal(400, result.StatusCode);
        }
    }

    public static class BaseApiControllerHelper
    {
        public static IActionResult GetErrorResponse(string message, int statusCode)
        {
            var ctrl = new PrivateController();
            var method = typeof(PrivateController).GetMethod("ErrorResponse",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return (IActionResult)method!.Invoke(ctrl, new object[] { message, statusCode, null! })!;
        }
        private class PrivateController : BaseApiController { }
    }

    public class AuthorizeAdminAttributeTests
    {
        [Fact]
        public void OnAuthorization_WithoutUser_Throws()
        {
            var ctx = CreateCtx(Array.Empty<Claim>());
            Assert.Throws<AppException>(() => new AuthorizeAdminAttribute().OnAuthorization(ctx));
        }

        [Fact]
        public void OnAuthorization_WithNonAdmin_Throws()
        {
            var ctx = CreateCtx(new[] { new Claim("roleId", "user") });
            Assert.Throws<AppException>(() => new AuthorizeAdminAttribute().OnAuthorization(ctx));
        }

        [Fact]
        public void OnAuthorization_WithAdmin_Succeeds()
        {
            var ctx = CreateCtx(new[] { new Claim("roleId", "administrator") });
            new AuthorizeAdminAttribute().OnAuthorization(ctx);
            Assert.Null(ctx.Result);
        }

        private static AuthorizationFilterContext CreateCtx(Claim[] claims)
        {
            var http = new DefaultHttpContext();
            http.Response.Body = new MemoryStream();
            if (claims.Length > 0)
            {
                http.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
            }
            return new AuthorizationFilterContext(
                new ActionContext(http, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()),
                new List<IFilterMetadata>());
        }
    }

    public class AuthControllerUnitTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly AuthController _controller;

        public AuthControllerUnitTests()
        {
            _controller = new AuthController(
                _mediator.Object,
                new LoginDtoValidator(),
                new RefreshDtoValidator(),
                new ResetRequestDtoValidator(),
                new ResetValidateDtoValidator(),
                new ChangePasswordDtoValidator());
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            _controller.HttpContext.Response.Body = new MemoryStream();
        }

        [Fact]
        public async Task Login_ValidRequest_CallsMediator()
        {
            _mediator.Setup(m => m.Send(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LoginResult(true, Response: new AuthResponseDto { Token = "t", User = new AuthUserDto { Email = "a@b.com" } }));

            var result = await _controller.Login(new LoginDto { Email = "a@b.com", Password = "p" }) as OkObjectResult;
            Assert.NotNull(result);
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public async Task Login_InvalidRequest_ThrowsValidationException()
        {
            var dto = new LoginDto { Email = "", Password = "" };
            await Assert.ThrowsAsync<ValidationException>(() => _controller.Login(dto));
        }

        [Fact]
        public async Task Refresh_ValidRequest_CallsMediator()
        {
            _mediator.Setup(m => m.Send(It.IsAny<RefreshTokenCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LoginResult(true, Response: new AuthResponseDto()));

            var result = await _controller.Refresh(new RefreshDto { RefreshToken = "rt" }) as OkObjectResult;
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetMe_WithoutUser_ReturnsError()
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetMeQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetMeResult(false, Error: "Not auth", StatusCode: 401));
            var result = await _controller.GetMe(null!);
            Assert.NotNull(result);
        }

        [Fact]
        public void Jwks_ReturnsEmptyKeys()
        {
            var result = _controller.Jwks() as OkObjectResult;
            Assert.NotNull(result);
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public async Task RequestPasswordReset_CallsMediator()
        {
            _mediator.Setup(m => m.Send(It.IsAny<RequestPasswordResetCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("token123");

            var result = await _controller.RequestPasswordReset(new ResetRequestDto { Email = "a@b.com" }) as OkObjectResult;
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Logout_WithUser_CallsMediator()
        {
            var identity = new ClaimsIdentity(new[] { new Claim("id", "u1") }, "test");
            _controller.HttpContext.User = new ClaimsPrincipal(identity);

            _mediator.Setup(m => m.Send(It.IsAny<LogoutCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Unit.Value);

            var result = await _controller.Logout() as OkObjectResult;
            Assert.NotNull(result);
        }
    }
}
