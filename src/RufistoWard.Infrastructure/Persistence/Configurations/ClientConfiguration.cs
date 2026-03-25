namespace RufistoWard.Tokens.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RufistoWard.Identity.Entities;

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.ClientId).HasMaxLength(200).IsRequired();
        builder.HasIndex(c => c.ClientId).IsUnique().HasDatabaseName("UQ_Clients_ClientId");

        builder.Property(c => c.ClientName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(1000);

        builder.HasMany(c => c.Secrets)
            .WithOne()
            .HasForeignKey(s => s.ClientId)
            .HasConstraintName("FK_ClientSecrets_Clients")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Scopes)
            .WithOne()
            .HasForeignKey(s => s.ClientId)
            .HasConstraintName("FK_ClientScopes_Clients")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.RedirectUris)
            .WithOne()
            .HasForeignKey(r => r.ClientId)
            .HasConstraintName("FK_ClientRedirectUris_Clients")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.GrantTypes)
            .WithOne()
            .HasForeignKey(g => g.ClientId)
            .HasConstraintName("FK_ClientGrantTypes_Clients")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
