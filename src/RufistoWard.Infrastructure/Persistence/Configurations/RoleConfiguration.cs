namespace RufistoWard.Tokens.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RufistoWard.Identity.Entities;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(r => r.Name).IsUnique().HasDatabaseName("UQ_Roles_Name");

        builder.Property(r => r.Description).HasMaxLength(1000);

        builder.HasMany(r => r.Permissions)
            .WithOne()
            .HasForeignKey(p => p.RoleId)
            .HasConstraintName("FK_RolePermissions_Roles")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
