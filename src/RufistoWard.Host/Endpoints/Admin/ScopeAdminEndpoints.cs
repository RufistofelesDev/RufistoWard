namespace RufistoWard.Core.Endpoints.Admin;

using Microsoft.EntityFrameworkCore;
using RufistoWard.Auth.Admin.Common;
using RufistoWard.Auth.Admin.Scopes;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;
using RufistoWard.Tokens.Persistence;

public static class ScopeAdminEndpoints
{
    public static IEndpointRouteBuilder MapScopeAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/scopes")
            .RequireAuthorization();

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
                .Select(s => new ScopeResponse
                {
                    Id = s.Id, Name = s.Name, DisplayName = s.DisplayName,
                    Description = s.Description, Enabled = s.Enabled,
                    Required = s.Required, Emphasize = s.Emphasize,
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(new PagedResult<ScopeResponse>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
            });
        });

        group.MapGet("/{id:int}", async (int id, RufistoWardDbContext db, CancellationToken ct) =>
        {
            var scope = await db.ApiScopes.FindAsync([id], ct);
            if (scope is null) return Results.NotFound();
            return Results.Ok(new ScopeResponse
            {
                Id = scope.Id, Name = scope.Name, DisplayName = scope.DisplayName,
                Description = scope.Description, Enabled = scope.Enabled,
                Required = scope.Required, Emphasize = scope.Emphasize,
            });
        });

        group.MapPost("/", async (
            CreateScopeRequest request,
            IScopeRepository repo,
            CancellationToken ct) =>
        {
            var existing = await repo.FindByNameAsync(request.Name, ct);
            if (existing is not null)
            {
                return Results.Conflict(new
                {
                    type = "https://tools.ietf.org/html/rfc9457",
                    title = "Scope already exists",
                    detail = $"A scope with name '{request.Name}' already exists.",
                    status = 409,
                });
            }

            var scope = ApiScope.Create(request.Name, request.DisplayName, request.Description);
            scope.Update(null, null, null, request.Required, request.Emphasize);

            await repo.AddAsync(scope, ct);

            return Results.Created($"/api/admin/scopes/{scope.Id}", new ScopeResponse
            {
                Id = scope.Id, Name = scope.Name, DisplayName = scope.DisplayName,
                Description = scope.Description, Enabled = scope.Enabled,
                Required = scope.Required, Emphasize = scope.Emphasize,
            });
        });

        group.MapPut("/{id:int}", async (
            int id,
            UpdateScopeRequest request,
            RufistoWardDbContext db,
            IScopeRepository repo,
            CancellationToken ct) =>
        {
            var scope = await db.ApiScopes.FindAsync([id], ct);
            if (scope is null) return Results.NotFound();

            scope.Update(request.DisplayName, request.Description, request.Enabled, request.Required, request.Emphasize);
            await repo.UpdateAsync(scope, ct);

            return Results.Ok(new ScopeResponse
            {
                Id = scope.Id, Name = scope.Name, DisplayName = scope.DisplayName,
                Description = scope.Description, Enabled = scope.Enabled,
                Required = scope.Required, Emphasize = scope.Emphasize,
            });
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
                Items = items.Select(r => (object)new
                {
                    r.Id, r.Name, r.DisplayName, r.Description, r.Enabled,
                    Scopes = r.Scopes.Select(s => s.Scope).ToArray(),
                }).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
            });
        });

        resourceGroup.MapGet("/{id:int}", async (int id, RufistoWardDbContext db, CancellationToken ct) =>
        {
            var resource = await db.ApiResources.Include(r => r.Scopes).FirstOrDefaultAsync(r => r.Id == id, ct);
            if (resource is null) return Results.NotFound();
            return Results.Ok(new
            {
                resource.Id, resource.Name, resource.DisplayName, resource.Description, resource.Enabled,
                Scopes = resource.Scopes.Select(s => s.Scope).ToArray(),
            });
        });

        resourceGroup.MapPost("/", async (
            CreateResourceRequest request,
            IResourceRepository repo,
            CancellationToken ct) =>
        {
            var existing = await repo.FindByNameAsync(request.Name, ct);
            if (existing is not null)
            {
                return Results.Conflict(new
                {
                    type = "https://tools.ietf.org/html/rfc9457",
                    title = "API resource already exists",
                    detail = $"An API resource with name '{request.Name}' already exists.",
                    status = 409,
                });
            }

            var resource = ApiResource.Create(request.Name, request.DisplayName, request.Description);
            foreach (var scope in request.Scopes) resource.AddScope(scope);

            await repo.AddAsync(resource, ct);

            return Results.Created($"/api/admin/api-resources/{resource.Id}", new
            {
                resource.Id, resource.Name, resource.DisplayName, resource.Description, resource.Enabled,
                Scopes = resource.Scopes.Select(s => s.Scope).ToArray(),
            });
        });

        resourceGroup.MapPut("/{id:int}", async (
            int id,
            UpdateResourceRequest request,
            RufistoWardDbContext db,
            IResourceRepository repo,
            CancellationToken ct) =>
        {
            var resource = await db.ApiResources.Include(r => r.Scopes).FirstOrDefaultAsync(r => r.Id == id, ct);
            if (resource is null) return Results.NotFound();

            resource.Update(request.DisplayName, request.Description, request.Enabled);

            if (request.Scopes is not null)
            {
                var currentScopes = resource.Scopes.Select(s => s.Scope).ToHashSet();
                var desired = request.Scopes.ToHashSet();
                foreach (var remove in currentScopes.Except(desired)) resource.RemoveScope(remove);
                foreach (var add in desired.Except(currentScopes)) resource.AddScope(add);
            }

            await repo.UpdateAsync(resource, ct);

            return Results.Ok(new
            {
                resource.Id, resource.Name, resource.DisplayName, resource.Description, resource.Enabled,
                Scopes = resource.Scopes.Select(s => s.Scope).ToArray(),
            });
        });

        resourceGroup.MapDelete("/{id:int}", async (int id, RufistoWardDbContext db, CancellationToken ct) =>
        {
            var resource = await db.ApiResources.FindAsync([id], ct);
            if (resource is null) return Results.NotFound();
            db.ApiResources.Remove(resource);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        return endpoints;
    }
}

public sealed record CreateResourceRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string Name { get; init; } = default!;

    [System.Text.Json.Serialization.JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("description")]
    public string? Description { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("scopes")]
    public string[] Scopes { get; init; } = [];
}

public sealed record UpdateResourceRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("description")]
    public string? Description { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("enabled")]
    public bool? Enabled { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("scopes")]
    public string[]? Scopes { get; init; }
}
