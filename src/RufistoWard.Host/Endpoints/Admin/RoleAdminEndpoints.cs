namespace RufistoWard.Core.Endpoints.Admin;

using Microsoft.EntityFrameworkCore;
using RufistoWard.Auth.Admin.Common;
using RufistoWard.Tokens.Persistence;

public static class RoleAdminEndpoints
{
    public static IEndpointRouteBuilder MapRoleAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/roles");

        group.MapGet("/", async (
            RufistoWardDbContext db,
            int page = 1, int pageSize = 25, string? search = null,
            CancellationToken cancellationToken = default) =>
        {
            var query = db.Roles.Include(r => r.Permissions).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(r => r.Name.Contains(search));
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderBy(r => r.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return Results.Ok(new PagedResult<object>
            {
                Items = items.Select(r => new
                {
                    r.Id, r.Name, r.Description,
                    Permissions = r.Permissions.Select(p => p.Permission).ToArray(),
                }).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
            });
        });

        group.MapGet("/{id:int}", async (int id, RufistoWardDbContext db, CancellationToken ct) =>
        {
            var role = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id, ct);
            if (role is null) return Results.NotFound();
            return Results.Ok(new
            {
                role.Id, role.Name, role.Description,
                Permissions = role.Permissions.Select(p => p.Permission).ToArray(),
            });
        });

        group.MapDelete("/{id:int}", async (int id, RufistoWardDbContext db, CancellationToken ct) =>
        {
            var role = await db.Roles.FindAsync([id], ct);
            if (role is null) return Results.NotFound();

            // Check if role has assigned users
            var hasUsers = await db.UserRoles.AnyAsync(ur => ur.RoleId == id, ct);
            if (hasUsers)
            {
                return Results.Conflict(new
                {
                    type = "https://tools.ietf.org/html/rfc9457",
                    title = "Role is in use",
                    detail = "Cannot delete a role that has users assigned to it.",
                    status = 409,
                });
            }

            db.Roles.Remove(role);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        return endpoints;
    }
}
