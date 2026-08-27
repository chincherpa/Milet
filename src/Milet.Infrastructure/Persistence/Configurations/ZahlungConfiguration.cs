using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Finanzen;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class ZahlungConfiguration : IEntityTypeConfiguration<Zahlung>
{
    public void Configure(EntityTypeBuilder<Zahlung> b)
    {
        b.ToTable("Zahlungen", t => t.HasCheckConstraint(
            "CK_Zahlungen_KundeOderLieferant",
            "([KundeId] IS NOT NULL AND [LieferantId] IS NULL) OR ([KundeId] IS NULL AND [LieferantId] IS NOT NULL)"));
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Kunde).WithMany().HasForeignKey(x => x.KundeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Lieferant).WithMany().HasForeignKey(x => x.LieferantId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Zuordnungen).WithOne(z => z.Zahlung).HasForeignKey(z => z.ZahlungId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Gesamtbetrag).HasPrecision(18, 2);
        b.Property(x => x.Zahlungsart).HasMaxLength(50);
        b.Property(x => x.Referenz).HasMaxLength(200);
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class ZahlungZuordnungConfiguration : IEntityTypeConfiguration<ZahlungZuordnung>
{
    public void Configure(EntityTypeBuilder<ZahlungZuordnung> b)
    {
        b.ToTable("ZahlungZuordnungen");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.OffenerPosten).WithMany().HasForeignKey(x => x.OffenerPostenId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Betrag).HasPrecision(18, 2);
        b.Property(x => x.SkontoBetrag).HasPrecision(18, 2);
    }
}
