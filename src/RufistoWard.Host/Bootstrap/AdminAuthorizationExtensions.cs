namespace RufistoWard.Core.Bootstrap;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RufistoWard.Tokens.Persistence;

/// <summary>
/// Who may use the admin UI and the admin API: an active user holding the reserved
/// <see cref="RoleName"/> role. Being signed in is not enough — every end user of every client
/// signs in to this same cookie.
/// </summary>
public static class AdminAuthorization
{
    public const string Policy = "RufistoWardAdmin";
    public const string RoleName = "rufistoward-admin";

    public static IServiceCollection AddRufistoWardAdminAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, AdminRequirementHandler>();
        services.AddAuthorizationBuilder()
            .AddPolicy(Policy, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new AdminRequirement()));
        return services;
    }
}

public sealed class AdminRequirement : IAuthorizationRequirement;

/// <summary>
/// Checks the database on every request rather than a claim in the cookie. The cookie slides, so
/// a role stamped into it at sign-in would outlive a revocation or a deactivation for as long as
/// the session stays active.
/// </summary>
public sealed class AdminRequirementHandler(RufistoWardDbContext db) : AuthorizationHandler<AdminRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminRequirement requirement)
    {
        var subjectId = context.User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(subjectId))
        {
            return;
        }

        var isAdmin = await db.Users.AnyAsync(u =>
            u.SubjectId == subjectId
            && u.IsActive
            && u.Roles.Any(ur => db.Roles.Any(r => r.Id == ur.RoleId && r.Name == AdminAuthorization.RoleName)));

        if (isAdmin)
        {
            context.Succeed(requirement);
        }
    }
}
