using scp.filestorage.Data.Models;
using scp.filestorage.Security;
using SCP.StorageFSC.Controllers;
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
    }
}
