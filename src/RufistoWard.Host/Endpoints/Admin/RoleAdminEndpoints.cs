namespace RufistoWard.Core.Endpoints.Admin;

using Microsoft.EntityFrameworkCore;
using RufistoWard.Auth.Admin.Common;
using RufistoWard.Auth.Admin.Roles;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;
using RufistoWard.Tokens.Persistence;

public static class RoleAdminEndpoints
{
    public static IEndpointRouteBuilder MapRoleAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/roles")
            .RequireAuthorization();

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
                .Select(r => new RoleResponse
                {
                    Id = r.Id, Name = r.Name, Description = r.Description,
                    Permissions = r.Permissions.Select(p => p.Permission).ToArray(),
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(new PagedResult<RoleResponse>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
            });
        });

        group.MapGet("/{id:int}", async (int id, RufistoWardDbContext db, CancellationToken ct) =>
        {
            var role = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id, ct);
            if (role is null) return Results.NotFound();
            return Results.Ok(new RoleResponse
            {
                Id = role.Id, Name = role.Name, Description = role.Description,
                Permissions = role.Permissions.Select(p => p.Permission).ToArray(),
            });
        });

        group.MapPost("/", async (
            CreateRoleRequest request,
            IRoleRepository repo,
            CancellationToken ct) =>
        {
            var existing = await repo.FindByNameAsync(request.Name, ct);
            if (existing is not null)
            {
                return Results.Conflict(new
                {
                    type = "https://tools.ietf.org/html/rfc9457",
                    title = "Role already exists",
                    detail = $"A role with name '{request.Name}' already exists.",
                    status = 409,
                });
            }

            var role = Role.Create(request.Name, request.Description);
            foreach (var permission in request.Permissions) role.AddPermission(permission);

            await repo.AddAsync(role, ct);

            return Results.Created($"/api/admin/roles/{role.Id}", new RoleResponse
            {
                Id = role.Id, Name = role.Name, Description = role.Description,
                Permissions = role.Permissions.Select(p => p.Permission).ToArray(),
            });
        });

        group.MapPut("/{id:int}", async (
            int id,
            UpdateRoleRequest request,
            RufistoWardDbContext db,
            IRoleRepository repo,
            CancellationToken ct) =>
        {
            var role = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id, ct);
            if (role is null) return Results.NotFound();

            role.Update(request.Name, request.Description);

            if (request.Permissions is not null)
            {
                var current = role.Permissions.Select(p => p.Permission).ToHashSet();
                var desired = request.Permissions.ToHashSet();
                foreach (var remove in current.Except(desired)) role.RemovePermission(remove);
                foreach (var add in desired.Except(current)) role.AddPermission(add);
            }

            await repo.UpdateAsync(role, ct);

            return Results.Ok(new RoleResponse
            {
                Id = role.Id, Name = role.Name, Description = role.Description,
                Permissions = role.Permissions.Select(p => p.Permission).ToArray(),
            });
        });

        group.MapDelete("/{id:int}", async (int id, RufistoWardDbContext db, CancellationToken ct) =>
        {
            var role = await db.Roles.FindAsync([id], ct);
            if (role is null) return Results.NotFound();

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
