using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using scp.filestorage.Data.Models;
using scp.filestorage.Security;
using scp.filestorage.Services.Auth;
using SCP.StorageFSC.Controllers;
using SCP.StorageFSC.Data.Dto;
using System.Security.Claims;

namespace SCP.StorageFSC.Tests
{
    public sealed class AuthControllerTests
    {
        [Fact]
        public void CreatePrincipal_AdministratorRole_AddsWebUserAndAdminClaims()
        {
            var userId = Guid.CreateVersion7();

            var principal = AuthController.CreatePrincipal(
                userId,
                "Administrator",
                [SystemRoles.Administrator]);

            Assert.Equal(userId.ToString(), principal.FindFirstValue(ClaimTypes.NameIdentifier));
            Assert.Equal("Administrator", principal.FindFirstValue(ClaimTypes.Name));
            Assert.Equal(AuthType.WebApp, principal.FindFirstValue("auth_type"));
            Assert.True(principal.IsInRole(SystemRoles.Administrator));
            Assert.True(principal.IsInRole("Admin"));
            Assert.Contains(principal.FindAll("scope"), claim => claim.Value == "admin");
        }

        [Fact]
        public async Task ChangePassword_WhenRequestIsValid_ChangesPasswordForAuthenticatedUser()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var userId = Guid.CreateVersion7();
            var authenticationService = new FakeAuthenticationService { ChangePasswordResult = true };
            var controller = CreateController(authenticationService, AuthController.CreatePrincipal(userId, "User", []));

            var result = await controller.ChangePassword(
                new ChangePasswordRequest
                {
                    OldPassword = "old-password",
                    NewPassword = "new-password"
                },
                cancellationToken);

            Assert.IsType<NoContentResult>(result);
            Assert.Equal(userId, authenticationService.ChangePasswordUserId);
            Assert.Equal("old-password", authenticationService.ChangePasswordCurrentPassword);
            Assert.Equal("new-password", authenticationService.ChangePasswordNewPassword);
        }

        [Fact]
        public async Task ChangePassword_WhenOldPasswordIsInvalid_ReturnsUnauthorized()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var userId = Guid.CreateVersion7();
            var authenticationService = new FakeAuthenticationService { ChangePasswordResult = false };
            var controller = CreateController(authenticationService, AuthController.CreatePrincipal(userId, "User", []));

            var result = await controller.ChangePassword(
                new ChangePasswordRequest
                {
                    OldPassword = "wrong-password",
                    NewPassword = "new-password"
                },
                cancellationToken);

            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
            var error = Assert.IsType<ApiErrorResponse>(unauthorized.Value);
            Assert.Equal("InvalidCredentials", error.ErrorCode);
        }

        [Fact]
        public async Task ChangePassword_WhenUserIdClaimIsMissing_ReturnsUnauthorizedWithoutChangingPassword()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var authenticationService = new FakeAuthenticationService();
            var controller = CreateController(
                authenticationService,
                new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Test")));

            var result = await controller.ChangePassword(
                new ChangePasswordRequest
                {
                    OldPassword = "old-password",
                    NewPassword = "new-password"
                },
                cancellationToken);

            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
            var error = Assert.IsType<ApiErrorResponse>(unauthorized.Value);
            Assert.Equal("InvalidUser", error.ErrorCode);
            Assert.Null(authenticationService.ChangePasswordUserId);
        }

        private static AuthController CreateController(
            IAuthenticationService authenticationService,
            ClaimsPrincipal user)
        {
            return new AuthController(authenticationService)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = user
                    }
                }
            };
        }

        private sealed class FakeAuthenticationService : IAuthenticationService
        {
            public bool ChangePasswordResult { get; init; }
            public Guid? ChangePasswordUserId { get; private set; }
            public string? ChangePasswordCurrentPassword { get; private set; }
            public string? ChangePasswordNewPassword { get; private set; }

            public Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<VerifyTwoFactorResult> VerifyTwoFactorAsync(VerifyTwoFactorRequest request, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<VerifyTwoFactorResult> VerifyRecoveryCodeAsync(VerifyTwoFactorRequest request, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<AuthenticatorSetupResult> BeginEnableAuthenticatorAsync(Guid userId, string issuer, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<TwoFactorSetupStatus> ConfirmEnableAuthenticatorAsync(Guid userId, string code, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<bool> DisableTwoFactorAsync(Guid userId, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<bool> LockUserAsync(Guid userId, DateTime lockedUntilUtc, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<bool> UnlockUserAsync(Guid userId, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<bool> ChangePasswordAsync(
                Guid userId,
                string currentPassword,
                string newPassword,
                CancellationToken cancellationToken = default)
            {
                ChangePasswordUserId = userId;
                ChangePasswordCurrentPassword = currentPassword;
                ChangePasswordNewPassword = newPassword;

                return Task.FromResult(ChangePasswordResult);
            }

            public Task<IReadOnlyList<Role>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }
        }
    }
}
