using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using RufistoWard.Core.Bootstrap;
using RufistoWard.Core.Commands;
using RufistoWard.Core.Endpoints.Admin;
using RufistoWard.Core.Endpoints.Protocol;
using RufistoWard.Core.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRufistoWardConfiguration(builder.Configuration);
builder.Services.AddRufistoWardInfrastructure(builder.Configuration);
builder.Services.AddRufistoWardCaching();
builder.Services.AddRufistoWardDiscovery(builder.Configuration);
builder.Services.AddRufistoWardKeyManagement(builder.Configuration);
builder.Services.AddRufistoWardProtocol();
builder.Services.AddRufistoWardRateLimiting(builder.Configuration);
builder.Services.AddRufistoWardAutoMigration(builder.Configuration);
builder.Services.AddRufistoWardHealthChecks();

builder.Services.AddAuthentication("IdentityServerCookie")
    .AddCookie("IdentityServerCookie", options =>
    {
        options.LoginPath = "/login";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();
builder.Services.AddRazorPages();

var app = builder.Build();

// CLI command dispatch — run command and exit before HTTP pipeline
if (args.Length > 0)
{
    var exitCode = args[0] switch
    {
        "seed-admin" => await SeedAdminCommand.ExecuteAsync(app.Services, args),
        "migrate" => await MigrateCommand.ExecuteAsync(app.Services),
        "is4-migrate" => await Is4MigrateCommand.ExecuteAsync(app.Services, args),
        _ => -1,
    };

    if (exitCode >= 0)
    {
        return;
    }
}

app.UseSecurityHeaders();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapDiscoveryEndpoints();
app.MapTokenEndpoints();
app.MapAuthorizeEndpoints();
app.MapDeviceEndpoints();
app.MapRevocationEndpoints();
app.MapIntrospectionEndpoints();
app.MapUserInfoEndpoints();
app.MapEndSessionEndpoints();
app.MapClientAdminEndpoints();
app.MapScopeAdminEndpoints();
app.MapUserAdminEndpoints();
app.MapRoleAdminEndpoints();
app.MapRazorPages();

app.MapGet("/", () => Results.Redirect("/login"));

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.ToString(),
            }),
        };
        await context.Response.WriteAsJsonAsync(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
    },
});

app.Run();
