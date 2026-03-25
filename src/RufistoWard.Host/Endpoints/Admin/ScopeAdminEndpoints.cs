namespace RufistoWard.Core.Endpoints.Admin;

using Microsoft.EntityFrameworkCore;
using RufistoWard.Auth.Admin.Common;
using RufistoWard.Tokens.Persistence;

public static class ScopeAdminEndpoints
{
    public static IEndpointRouteBuilder MapScopeAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/scopes");

        group.MapGet("/", async (
            RufistoWardDbContext db,
            int page = 1, int pageSize = 25, string? search = null,
            CancellationToken cancellationToken = default) =>
        {
            var query = db.ApiScopes.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(s => s.Name.Contains(search) || (s.DisplayName != null && s.DisplayName.Contains(search)));
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderBy(s => s.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return Results.Ok(new PagedResult<object>
            {
                Items = items.Select(s => new { s.Id, s.Name, s.DisplayName, s.Description, s.Enabled }).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
            });
        });

        group.MapGet("/{id:int}", async (int id, RufistoWardDbContext db, CancellationToken ct) =>
        {
            var scope = await db.ApiScopes.FindAsync([id], ct);
            return scope is null ? Results.NotFound() : Results.Ok(scope);
        });

        group.MapDelete("/{id:int}", async (int id, RufistoWardDbContext db, CancellationToken ct) =>
        {
            var scope = await db.ApiScopes.FindAsync([id], ct);
            if (scope is null) return Results.NotFound();
            db.ApiScopes.Remove(scope);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // API Resources
        var resourceGroup = endpoints.MapGroup("/api/admin/api-resources");

        resourceGroup.MapGet("/", async (
            RufistoWardDbContext db,
            int page = 1, int pageSize = 25, string? search = null,
            CancellationToken cancellationToken = default) =>
        {
            var query = db.ApiResources.Include(r => r.Scopes).AsQueryable();
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
                Items = items.Select(r => new { r.Id, r.Name, r.DisplayName, r.Description, r.Enabled, Scopes = r.Scopes.Select(s => s.Scope) }).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
            });
        });

        return endpoints;
    }
}
