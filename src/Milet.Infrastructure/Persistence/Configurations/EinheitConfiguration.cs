using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Domain.Entities.Stammdaten;

namespace Nexus.Infrastructure.Persistence.Configurations;

public sealed class EinheitConfiguration : IEntityTypeConfiguration<Einheit>
{
    public void Configure(EntityTypeBuilder<Einheit> b)
    {
        b.ToTable("Einheiten");
        b.HasKey(e => e.Id);
        b.Property(e => e.Kuerzel).HasMaxLength(10).IsRequired();
        b.Property(e => e.Bezeichnung).HasMaxLength(50).IsRequired();
        b.HasIndex(e => e.Kuerzel).IsUnique();
    }
}
