using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using MageBackend.Features.Auth;
using MageBackend.Features.Auth.Commands;
using MageBackend.Features.Auth.Queries;
using MageBackend.Infrastructure.Auth;

namespace MageBackend.Tests
{
    public class AuthControllerFullTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly AuthController _controller;

        public AuthControllerFullTests()
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
        public async Task Login_InvalidBody_Throws()
        {
            await Assert.ThrowsAsync<ValidationException>(() =>
                _controller.Login(new LoginDto { Email = "", Password = "" }));
        }

        [Fact]
        public async Task Login_MediatorFails_ReturnsError()
        {
            _mediator.Setup(m => m.Send(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LoginResult(false, Error: "bad", ErrorKey: "UnauthorizedError", StatusCode: 401));

            var result = await _controller.Login(new LoginDto { Email = "a@b.com", Password = "p" }) as ObjectResult;
            Assert.NotNull(result);
            Assert.Equal(401, result.StatusCode);
        }

        [Fact]
        public async Task Login_MediatorSucceeds_ReturnsOk()
        {
            _mediator.Setup(m => m.Send(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LoginResult(true, Response: new AuthResponseDto { Token = "t" }));

            var result = await _controller.Login(new LoginDto { Email = "a@b.com", Password = "p" }) as OkObjectResult;
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Refresh_InvalidBody_Throws()
        {
            await Assert.ThrowsAsync<ValidationException>(() =>
                _controller.Refresh(new RefreshDto { RefreshToken = "" }));
        }

        [Fact]
        public async Task Refresh_MediatorFails_ReturnsError()
        {
            _mediator.Setup(m => m.Send(It.IsAny<RefreshTokenCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LoginResult(false, Error: "bad", StatusCode: 401));

            var result = await _controller.Refresh(new RefreshDto { RefreshToken = "rt" }) as ObjectResult;
            Assert.Equal(401, result?.StatusCode);
        }

        [Fact]
        public async Task GetMe_WithUser_CallsMediator()
        {
            var identity = new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim("id", "u1") }, "test");
            _controller.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(identity);

            _mediator.Setup(m => m.Send(It.IsAny<GetMeQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetMeResult(true, Response: new AuthResponseDto { User = new AuthUserDto { Email = "a@b.com" } }));

            var result = await _controller.GetMe("Bearer tok") as OkObjectResult;
            Assert.NotNull(result);
        }

        [Fact]
        public async Task PasswordRequest_Invalid_Throws()
        {
            await Assert.ThrowsAsync<ValidationException>(() =>
                _controller.RequestPasswordReset(new ResetRequestDto { Email = "" }));
        }

        [Fact]
        public async Task PasswordRequest_Valid_ReturnsOk()
        {
            _mediator.Setup(m => m.Send(It.IsAny<RequestPasswordResetCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("token123");

            var result = await _controller.RequestPasswordReset(new ResetRequestDto { Email = "a@b.com" }) as OkObjectResult;
            Assert.NotNull(result);
        }

        [Fact]
        public async Task PasswordValidate_Invalid_Throws()
        {
            await Assert.ThrowsAsync<ValidationException>(() =>
                _controller.ValidateResetToken(new ResetValidateDto { Email = "", Token = "" }));
        }

        [Fact]
        public async Task PasswordChange_Invalid_Throws()
        {
            await Assert.ThrowsAsync<ValidationException>(() =>
                _controller.ChangePassword(new ChangePasswordDto { Email = "", Token = "", Password = "" }));
        }

        [Fact]
        public async Task PasswordChange_MediatorFails_ReturnsError()
        {
            _mediator.Setup(m => m.Send(It.IsAny<ChangePasswordCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChangePasswordResult(false, Error: "bad", StatusCode: 401));

            var result = await _controller.ChangePassword(new ChangePasswordDto { Email = "a@b.com", Token = "t", Password = "p" }) as ObjectResult;
            Assert.Equal(401, result?.StatusCode);
        }

        [Fact]
        public void Jwks_ReturnsEmptyKeys()
        {
            var result = _controller.Jwks() as OkObjectResult;
            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        [Fact]
        public async Task Logout_WithUser_CallsMediator()
        {
            var identity = new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim("id", "u1") }, "test");
            _controller.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(identity);

            _mediator.Setup(m => m.Send(It.IsAny<LogoutCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Unit.Value);

            var result = await _controller.Logout() as OkObjectResult;
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Logout_WithoutUser_ReturnsOk()
        {
            var result = await _controller.Logout() as OkObjectResult;
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetMe_WithToken_ReturnsOk()
        {
            var identity = new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim("id", "u1") }, "test");
            _controller.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(identity);

            _mediator.Setup(m => m.Send(It.IsAny<GetMeQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetMeResult(true, Response: new AuthResponseDto()));

            var result = await _controller.GetMe("Bearer token123") as OkObjectResult;
            Assert.NotNull(result);
        }
    }
}
