using Microsoft.Extensions.DependencyInjection;

namespace scp.filestorage.Data.Repositories
{
    public static class UserManagementRepositoryServiceCollectionExtensions
    {
        public static IServiceCollection AddUserManagementRepositories(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IUserTwoFactorMethodRepository, UserTwoFactorMethodRepository>();
            services.AddScoped<IUserTwoFactorChallengeRepository, UserTwoFactorChallengeRepository>();
            services.AddScoped<IUserRecoveryCodeRepository, UserRecoveryCodeRepository>();
            services.AddScoped<IUserLoginChallengeRepository, UserLoginChallengeRepository>();
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<IUserRoleRepository, UserRoleRepository>();

            return services;
        }
    }
}
