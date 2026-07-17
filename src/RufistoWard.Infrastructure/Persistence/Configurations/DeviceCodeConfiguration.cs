namespace RufistoWard.Tokens.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RufistoWard.Identity.Entities;

public sealed class DeviceCodeConfiguration : IEntityTypeConfiguration<DeviceCode>
{
    public void Configure(EntityTypeBuilder<DeviceCode> builder)
    {
        builder.ToTable("DeviceCodes");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.DeviceCodeValue).HasMaxLength(200).IsRequired();
        builder.HasIndex(d => new { d.TenantId, d.DeviceCodeValue }).IsUnique().HasDatabaseName("UQ_DeviceCodes_TenantId_DeviceCode");

        builder.Property(d => d.UserCode).HasMaxLength(200).IsRequired();
        builder.HasIndex(d => new { d.TenantId, d.UserCode }).IsUnique().HasDatabaseName("UQ_DeviceCodes_TenantId_UserCode");

        builder.Property(d => d.SubjectId).HasMaxLength(200);
        builder.Property(d => d.ClientId).HasMaxLength(200).IsRequired();
        builder.Property(d => d.Data).IsRequired();
    }
}
