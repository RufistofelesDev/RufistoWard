namespace RufistoWard.Tokens.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RufistoWard.Identity.Entities;

public sealed class ApiResourceConfiguration : IEntityTypeConfiguration<ApiResource>
{
    public void Configure(EntityTypeBuilder<ApiResource> builder)
    {
        builder.ToTable("ApiResources");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(r => r.Name).IsUnique().HasDatabaseName("UQ_ApiResources_Name");

        builder.Property(r => r.DisplayName).HasMaxLength(200);
        builder.Property(r => r.Description).HasMaxLength(1000);

        builder.HasMany(r => r.Scopes)
            .WithOne()
            .HasForeignKey(s => s.ApiResourceId)
            .HasConstraintName("FK_ApiResourceScopes_ApiResources")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
