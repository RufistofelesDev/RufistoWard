namespace RufistoWard.Tokens.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RufistoWard.Identity.Entities;

public sealed class ApiResourceScopeConfiguration : IEntityTypeConfiguration<ApiResourceScope>
{
    public void Configure(EntityTypeBuilder<ApiResourceScope> builder)
    {
        builder.ToTable("ApiResourceScopes");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Scope).HasMaxLength(200).IsRequired();
    }
}
