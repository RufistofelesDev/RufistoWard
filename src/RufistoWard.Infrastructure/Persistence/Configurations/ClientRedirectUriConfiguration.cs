namespace RufistoWard.Tokens.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RufistoWard.Identity.Entities;

public sealed class ClientRedirectUriConfiguration : IEntityTypeConfiguration<ClientRedirectUri>
{
    public void Configure(EntityTypeBuilder<ClientRedirectUri> builder)
    {
        builder.ToTable("ClientRedirectUris");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Uri).HasMaxLength(2000).IsRequired();
    }
}
