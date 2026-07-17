namespace RufistoWard.Tokens.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RufistoWard.Identity.Entities;

public sealed class SigningKeyConfiguration : IEntityTypeConfiguration<SigningKey>
{
    public void Configure(EntityTypeBuilder<SigningKey> builder)
    {
        builder.ToTable("SigningKeys");
        builder.HasKey(k => k.Id);

        builder.Property(k => k.KeyId).HasMaxLength(200).IsRequired();
        builder.HasIndex(k => new { k.TenantId, k.KeyId }).IsUnique().HasDatabaseName("UQ_SigningKeys_TenantId_KeyId");

        builder.Property(k => k.Algorithm).HasMaxLength(50).IsRequired();
        builder.Property(k => k.KeyType).HasMaxLength(50).IsRequired();
        builder.Property(k => k.SerializedKey).IsRequired();
    }
}
