using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using scp.filestorage.Data.Models;
using scp.filestorage.Security;
using scp.filestorage.Services.Auth;
using SCP.StorageFSC.Controllers;
using SCP.StorageFSC.Data.Dto;
using SCP.StorageFSC.InterfacesService;
using System.Net;
using System.Security.Claims;
using AuthLoginRequest = scp.filestorage.Services.Auth.LoginRequest;

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

        [Fact]
        public async Task Login_WhenInvalidCredentials_LogsAuditFailureWithClientContext()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var authenticationService = new FakeAuthenticationService
            {
                LoginResult = new LoginResult
                {
                    Status = AuthLoginStatus.InvalidCredentials
                }
            };
            var auditService = new FakeUserAuthenticationAuditService();
            var controller = CreateController(
                authenticationService,
                new ClaimsPrincipal(new ClaimsIdentity()),
                auditService);

            controller.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.5");
            controller.HttpContext.Request.Path = "/auth/login";

            var result = await controller.Login(
                new AuthLoginRequest
                {
                    Login = "user@example.com",
                    Password = "wrong-password"
                },
                cancellationToken);

            Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal("user@example.com", auditService.PasswordLoginLogin);
            Assert.NotNull(auditService.PasswordLoginResult);
            Assert.Equal(AuthLoginStatus.InvalidCredentials, auditService.PasswordLoginResult.Status);
        }

        [Fact]
        public async Task VerifyTwoFactor_WhenCodeIsInvalid_LogsTwoFactorAuditFailure()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var authenticationService = new FakeAuthenticationService
            {
                VerifyTwoFactorResult = new VerifyTwoFactorResult
                {
                    Status = TwoFactorVerifyStatus.InvalidCode
                }
            };
            var auditService = new FakeUserAuthenticationAuditService();
            var controller = CreateController(
                authenticationService,
                new ClaimsPrincipal(new ClaimsIdentity()),
                auditService);

            var result = await controller.VerifyTwoFactor(
                new VerifyTwoFactorLoginRequest
                {
                    ChallengeToken = "challenge",
                    Code = "000000"
                },
                cancellationToken);

            Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal("TwoFactor", auditService.TwoFactorEventType);
            Assert.NotNull(auditService.TwoFactorResult);
            Assert.Equal(TwoFactorVerifyStatus.InvalidCode, auditService.TwoFactorResult.Status);
        }

        private static AuthController CreateController(
            IAuthenticationService authenticationService,
            ClaimsPrincipal user,
            IUserAuthenticationAuditService? auditService = null)
        {
            return new AuthController(
                authenticationService,
                auditService ?? new FakeUserAuthenticationAuditService())
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
            public LoginResult LoginResult { get; init; } = new();
            public VerifyTwoFactorResult VerifyTwoFactorResult { get; init; } = new();
            public bool ChangePasswordResult { get; init; }
            public Guid? ChangePasswordUserId { get; private set; }
            public string? ChangePasswordCurrentPassword { get; private set; }
            public string? ChangePasswordNewPassword { get; private set; }

            public Task<LoginResult> LoginAsync(AuthLoginRequest request, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(LoginResult);
            }

            public Task<VerifyTwoFactorResult> VerifyTwoFactorAsync(VerifyTwoFactorRequest request, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(VerifyTwoFactorResult);
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

        private sealed class FakeUserAuthenticationAuditService : IUserAuthenticationAuditService
        {
            public string? PasswordLoginLogin { get; private set; }
            public LoginResult? PasswordLoginResult { get; private set; }
            public string? TwoFactorEventType { get; private set; }
            public VerifyTwoFactorResult? TwoFactorResult { get; private set; }

            public Task LogPasswordLoginAsync(
                HttpContext context,
                string login,
                LoginResult result,
                CancellationToken cancellationToken = default)
            {
                PasswordLoginLogin = login;
                PasswordLoginResult = result;
                return Task.CompletedTask;
            }

            public Task LogTwoFactorAsync(
                HttpContext context,
                VerifyTwoFactorResult result,
                string eventType,
                CancellationToken cancellationToken = default)
            {
                TwoFactorResult = result;
                TwoFactorEventType = eventType;
                return Task.CompletedTask;
            }

            public Task<UserAuthenticationNotificationPageDto> GetNotificationsAsync(
                Guid? userId = null,
                int pageNumber = 1,
                int pageSize = 20,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new UserAuthenticationNotificationPageDto
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = 0,
                    Items = []
                });
            }
        }
    }
}
