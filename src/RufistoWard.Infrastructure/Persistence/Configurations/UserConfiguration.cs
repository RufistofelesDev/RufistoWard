namespace RufistoWard.Tokens.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RufistoWard.Identity.Entities;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.SubjectId).HasMaxLength(200).IsRequired();
        builder.HasIndex(u => new { u.TenantId, u.SubjectId }).IsUnique().HasDatabaseName("UQ_Users_TenantId_SubjectId");

        builder.Property(u => u.Username).HasMaxLength(200).IsRequired();
        builder.HasIndex(u => new { u.TenantId, u.Username }).IsUnique().HasDatabaseName("UQ_Users_TenantId_Username");

        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.HasIndex(u => new { u.TenantId, u.Email }).IsUnique().HasDatabaseName("IX_Users_TenantId_Email");

        builder.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();

        builder.HasMany(u => u.Claims)
            .WithOne()
            .HasForeignKey(c => c.UserId)
            .HasConstraintName("FK_UserClaims_Users")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.Roles)
            .WithOne()
            .HasForeignKey(r => r.UserId)
            .HasConstraintName("FK_UserRoles_Users")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
