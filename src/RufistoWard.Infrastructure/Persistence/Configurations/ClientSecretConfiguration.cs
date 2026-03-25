namespace RufistoWard.Tokens.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RufistoWard.Identity.Entities;

public sealed class ClientSecretConfiguration : IEntityTypeConfiguration<ClientSecret>
{
    public void Configure(EntityTypeBuilder<ClientSecret> builder)
    {
        builder.ToTable("ClientSecrets");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Value).HasMaxLength(4000).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(1000);
    }
}
