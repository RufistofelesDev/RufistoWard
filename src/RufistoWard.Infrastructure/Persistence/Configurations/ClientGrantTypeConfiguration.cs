namespace RufistoWard.Tokens.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RufistoWard.Identity.Entities;

public sealed class ClientGrantTypeConfiguration : IEntityTypeConfiguration<ClientGrantType>
{
    public void Configure(EntityTypeBuilder<ClientGrantType> builder)
    {
        builder.ToTable("ClientGrantTypes");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.GrantType).HasMaxLength(250).IsRequired();
    }
}
