using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Stammdaten;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class ZahlungsbedingungConfiguration : IEntityTypeConfiguration<Zahlungsbedingung>
{
    public void Configure(EntityTypeBuilder<Zahlungsbedingung> b)
    {
        b.ToTable("Zahlungsbedingungen");
        b.HasKey(z => z.Id);
        b.Property(z => z.Bezeichnung).HasMaxLength(100).IsRequired();
        b.Property(z => z.SkontoProzent).HasPrecision(5, 2);
    }
}
