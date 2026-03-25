namespace RufistoWard.Core.Options;

using System.ComponentModel.DataAnnotations;

public enum DatabaseProvider
{
    PostgreSQL,
    SqlServer,
}

public sealed record DatabaseOptions
{
    public DatabaseProvider Provider { get; init; } = DatabaseProvider.PostgreSQL;

    [Required]
    public string ConnectionString { get; init; } = default!;

    public bool AutoMigrateOnStartup { get; init; }
}
