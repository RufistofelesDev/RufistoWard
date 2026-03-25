namespace RufistoWard.Tokens.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RufistoWard.Identity.Entities;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Permission).HasMaxLength(200).IsRequired();
    }
}
