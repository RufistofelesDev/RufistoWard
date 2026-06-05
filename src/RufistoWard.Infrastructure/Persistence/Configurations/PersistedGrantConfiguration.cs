namespace RufistoWard.Tokens.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RufistoWard.Identity.Entities;

public sealed class PersistedGrantConfiguration : IEntityTypeConfiguration<PersistedGrant>
{
    public void Configure(EntityTypeBuilder<PersistedGrant> builder)
    {
        builder.ToTable("PersistedGrants");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id).HasMaxLength(200);
        builder.Property(g => g.Type).HasMaxLength(50).IsRequired();
        builder.Property(g => g.SubjectId).HasMaxLength(200);
        builder.Property(g => g.ClientId).HasMaxLength(200).IsRequired();
        builder.Property(g => g.Data).IsRequired();
        builder.Property(g => g.FamilyId).IsRequired();

        builder.HasIndex(g => g.SubjectId).HasDatabaseName("IX_PersistedGrants_SubjectId");
        builder.HasIndex(g => g.ClientId).HasDatabaseName("IX_PersistedGrants_ClientId");
        builder.HasIndex(g => g.Expiration).HasDatabaseName("IX_PersistedGrants_Expiration");
        builder.HasIndex(g => new { g.FamilyId, g.ConsumedAt }).HasDatabaseName("IX_PersistedGrants_FamilyId_ConsumedAt");
    }
}
