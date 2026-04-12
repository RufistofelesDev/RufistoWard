namespace RufistoWard.Core.Endpoints.Admin;

using Microsoft.EntityFrameworkCore;
using RufistoWard.Auth.Admin.Clients;
using RufistoWard.Auth.Admin.Common;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;
using RufistoWard.Tokens.Persistence;

public static class ClientAdminEndpoints
{
    public static IEndpointRouteBuilder MapClientAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/clients")
            .RequireAuthorization();

        group.MapGet("/", async (
            RufistoWardDbContext db,
            int page = 1, int pageSize = 25, string? search = null,
            CancellationToken cancellationToken = default) =>
        {
            var query = db.Clients
                .Include(c => c.Scopes)
                .Include(c => c.GrantTypes)
                .Include(c => c.RedirectUris)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c => c.ClientName.Contains(search) || c.ClientId.Contains(search));
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderBy(c => c.ClientName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new ClientResponse
                {
                    Id = c.Id,
                    ClientId = c.ClientId,
                    ClientName = c.ClientName,
                    Enabled = c.Enabled,
                    GrantTypes = c.GrantTypes.Select(g => g.GrantType).ToArray(),
                    RedirectUris = c.RedirectUris.Select(r => r.Uri).ToArray(),
                    AllowedScopes = c.Scopes.Select(s => s.Scope).ToArray(),
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(new PagedResult<ClientResponse>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
            });
        });

        group.MapGet("/{clientId}", async (
            string clientId,
            IClientRepository repo,
            CancellationToken cancellationToken) =>
        {
            var client = await repo.FindByClientIdAsync(clientId, cancellationToken);
            if (client is null) return Results.NotFound();

            return Results.Ok(new ClientResponse
            {
                Id = client.Id,
                ClientId = client.ClientId,
                ClientName = client.ClientName,
                Enabled = client.Enabled,
                GrantTypes = client.GrantTypes.Select(g => g.GrantType).ToArray(),
                RedirectUris = client.RedirectUris.Select(r => r.Uri).ToArray(),
                AllowedScopes = client.Scopes.Select(s => s.Scope).ToArray(),
            });
        });

        group.MapPost("/", async (
            CreateClientRequest request,
            IClientRepository repo,
            CancellationToken cancellationToken) =>
        {
            var clientId = Guid.NewGuid().ToString("N")[..16];
            var client = Client.Create(clientId, request.ClientName, request.RequireClientSecret);

            foreach (var gt in request.GrantTypes) client.AddGrantType(gt);
            foreach (var uri in request.RedirectUris) client.AddRedirectUri(uri);
            foreach (var scope in request.AllowedScopes) client.AddScope(scope);

            string? plainSecret = null;
            if (request.RequireClientSecret)
            {
                plainSecret = Guid.NewGuid().ToString("N");
                client.AddSecret(Convert.ToBase64String(
                    System.Security.Cryptography.SHA256.HashData(
                        System.Text.Encoding.UTF8.GetBytes(plainSecret))));
            }

            await repo.AddAsync(client, cancellationToken);

            return Results.Created($"/api/admin/clients/{clientId}", new ClientResponse
            {
                Id = client.Id,
                ClientId = client.ClientId,
                ClientName = client.ClientName,
                Enabled = client.Enabled,
                GrantTypes = client.GrantTypes.Select(g => g.GrantType).ToArray(),
                RedirectUris = client.RedirectUris.Select(r => r.Uri).ToArray(),
                AllowedScopes = client.Scopes.Select(s => s.Scope).ToArray(),
                ClientSecret = plainSecret,
            });
        });

        group.MapPut("/{clientId}", async (
            string clientId,
            UpdateClientRequest request,
            IClientRepository repo,
            CancellationToken cancellationToken) =>
        {
            var client = await repo.FindByClientIdAsync(clientId, cancellationToken);
            if (client is null) return Results.NotFound();

            client.Update(request.ClientName, request.Enabled, request.RequirePkce,
                request.AccessTokenLifetimeSeconds, request.RefreshTokenLifetimeSeconds);

            await repo.UpdateAsync(client, cancellationToken);

            return Results.Ok(new ClientResponse
            {
                Id = client.Id,
                ClientId = client.ClientId,
                ClientName = client.ClientName,
                Enabled = client.Enabled,
                GrantTypes = client.GrantTypes.Select(g => g.GrantType).ToArray(),
                RedirectUris = client.RedirectUris.Select(r => r.Uri).ToArray(),
                AllowedScopes = client.Scopes.Select(s => s.Scope).ToArray(),
            });
        });

        group.MapDelete("/{clientId}", async (
            string clientId,
            IClientRepository repo,
            CancellationToken cancellationToken) =>
        {
            var client = await repo.FindByClientIdAsync(clientId, cancellationToken);
            if (client is null) return Results.NotFound();

            await repo.DeleteAsync(client, cancellationToken);
            return Results.NoContent();
        });

        return endpoints;
    }
}
