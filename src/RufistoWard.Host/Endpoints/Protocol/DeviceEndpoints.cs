namespace RufistoWard.Core.Endpoints.Protocol;

using RufistoWard.Auth.Protocol.Device;

public static class DeviceEndpoints
{
    public static IEndpointRouteBuilder MapDeviceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/connect/deviceauthorization", async (
            HttpContext httpContext,
            DeviceAuthorizationHandler handler,
            CancellationToken cancellationToken) =>
        {
            var form = await httpContext.Request.ReadFormAsync(cancellationToken);
            var clientId = form["client_id"].ToString();
            var scope = form["scope"].ToString();

            var result = await handler.HandleAsync(clientId, scope, cancellationToken);

            if (result.IsSuccess)
            {
                return Results.Ok(result.Value);
            }

            return Results.Json(
                new { error = result.Error!.Code, error_description = result.Error.Description },
                statusCode: result.Error.HttpStatus);
        });

        return endpoints;
    }
}
