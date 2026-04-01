namespace RufistoWard.Core.Endpoints.Admin;

using Microsoft.EntityFrameworkCore;
using RufistoWard.Auth.Admin.Common;
using RufistoWard.Auth.Admin.Users;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;
using RufistoWard.Tokens.Persistence;

public static class UserAdminEndpoints
{
    public static IEndpointRouteBuilder MapUserAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/users");

        group.MapGet("/", async (
            RufistoWardDbContext db,
            int page = 1, int pageSize = 25, string? search = null, string? status = null,
            CancellationToken cancellationToken = default) =>
        {
            var query = db.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u => u.Username.Contains(search) || u.Email.Contains(search));
            }

            if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(u => u.IsActive);
            }
            else if (string.Equals(status, "inactive", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(u => !u.IsActive);
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderBy(u => u.Username)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserResponse
                {
                    Id = u.Id, SubjectId = u.SubjectId, Username = u.Username,
                    Email = u.Email, IsActive = u.IsActive, CreatedAt = u.CreatedAt,
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(new PagedResult<UserResponse>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
            });
        });

        group.MapGet("/{subjectId}", async (
            string subjectId, IUserRepository repo, CancellationToken ct) =>
        {
            var user = await repo.FindBySubjectIdAsync(subjectId, ct);
            if (user is null) return Results.NotFound();
            return Results.Ok(new UserResponse
            {
                Id = user.Id, SubjectId = user.SubjectId, Username = user.Username,
                Email = user.Email, IsActive = user.IsActive, CreatedAt = user.CreatedAt,
            });
        });

        group.MapPost("/", async (
            CreateUserRequest request,
            IUserRepository repo,
            IPasswordHasher passwordHasher,
            CancellationToken ct) =>
        {
            var existingByUsername = await repo.FindByUsernameAsync(request.Username, ct);
            if (existingByUsername is not null)
            {
                return Results.Conflict(new
                {
                    type = "https://tools.ietf.org/html/rfc9457",
                    title = "Username already exists",
                    detail = $"A user with username '{request.Username}' already exists.",
                    status = 409,
                });
            }

            var existingByEmail = await repo.FindByEmailAsync(request.Email, ct);
            if (existingByEmail is not null)
            {
                return Results.Conflict(new
                {
                    type = "https://tools.ietf.org/html/rfc9457",
                    title = "Email already exists",
                    detail = $"A user with email '{request.Email}' already exists.",
                    status = 409,
                });
            }

            var subjectId = Guid.NewGuid().ToString();
            var hash = passwordHasher.Hash(request.Password);
            var user = User.Create(subjectId, request.Username, request.Email, hash);

            await repo.AddAsync(user, ct);

            return Results.Created($"/api/admin/users/{subjectId}", new UserResponse
            {
                Id = user.Id, SubjectId = user.SubjectId, Username = user.Username,
                Email = user.Email, IsActive = user.IsActive, CreatedAt = user.CreatedAt,
            });
        });

        group.MapPut("/{subjectId}", async (
            string subjectId,
            UpdateUserRequest request,
            IUserRepository repo,
            CancellationToken ct) =>
        {
            var user = await repo.FindBySubjectIdAsync(subjectId, ct);
            if (user is null) return Results.NotFound();

            user.Update(request.Username, request.Email, request.EmailConfirmed);
            await repo.UpdateAsync(user, ct);

            return Results.Ok(new UserResponse
            {
                Id = user.Id, SubjectId = user.SubjectId, Username = user.Username,
                Email = user.Email, IsActive = user.IsActive, CreatedAt = user.CreatedAt,
            });
        });

        group.MapPost("/{subjectId}/deactivate", async (
            string subjectId, IUserRepository repo, CancellationToken ct) =>
        {
            var user = await repo.FindBySubjectIdAsync(subjectId, ct);
            if (user is null) return Results.NotFound();

            user.Deactivate();
            await repo.UpdateAsync(user, ct);

            return Results.Ok(new UserResponse
            {
                Id = user.Id, SubjectId = user.SubjectId, Username = user.Username,
                Email = user.Email, IsActive = user.IsActive, CreatedAt = user.CreatedAt,
            });
        });

        group.MapPost("/{subjectId}/activate", async (
            string subjectId, IUserRepository repo, CancellationToken ct) =>
        {
            var user = await repo.FindBySubjectIdAsync(subjectId, ct);
            if (user is null) return Results.NotFound();

            user.Activate();
            await repo.UpdateAsync(user, ct);

            return Results.Ok(new UserResponse
            {
                Id = user.Id, SubjectId = user.SubjectId, Username = user.Username,
                Email = user.Email, IsActive = user.IsActive, CreatedAt = user.CreatedAt,
            });
        });

        return endpoints;
    }
}
