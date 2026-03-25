namespace RufistoWard.Tokens.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RufistoWard.Identity.Entities;

public sealed class ClientScopeConfiguration : IEntityTypeConfiguration<ClientScope>
{
    public void Configure(EntityTypeBuilder<ClientScope> builder)
    {
        builder.ToTable("ClientScopes");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Scope).HasMaxLength(200).IsRequired();
    }
}
