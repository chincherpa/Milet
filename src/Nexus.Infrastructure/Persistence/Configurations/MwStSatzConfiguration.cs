using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Domain.Entities.Stammdaten;

namespace Nexus.Infrastructure.Persistence.Configurations;

public sealed class MwStSatzConfiguration : IEntityTypeConfiguration<MwStSatz>
{
    public void Configure(EntityTypeBuilder<MwStSatz> b)
    {
        b.ToTable("MwStSaetze");
        b.HasKey(m => m.Id);
        b.Property(m => m.Bezeichnung).HasMaxLength(50).IsRequired();
        b.Property(m => m.Satz).HasPrecision(5, 2);
    }
}
