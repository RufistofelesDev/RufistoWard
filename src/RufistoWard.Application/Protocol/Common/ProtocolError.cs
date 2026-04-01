namespace RufistoWard.Auth.Protocol.Common;

public sealed record ProtocolError(string Code, string Description, int HttpStatus = 400)
{
    public static ProtocolError InvalidRequest(string description) => new("invalid_request", description);
    public static ProtocolError InvalidClient(string description) => new("invalid_client", description, 401);
    public static ProtocolError InvalidGrant(string description) => new("invalid_grant", description);
    public static ProtocolError InvalidScope(string description) => new("invalid_scope", description);
    public static ProtocolError UnauthorizedClient(string description) => new("unauthorized_client", description);
    public static ProtocolError UnsupportedGrantType(string description) => new("unsupported_grant_type", description);
    public static ProtocolError InvalidToken(string description) => new("invalid_token", description, 401);
    public static ProtocolError InsufficientScope(string description) => new("insufficient_scope", description, 403);
}
