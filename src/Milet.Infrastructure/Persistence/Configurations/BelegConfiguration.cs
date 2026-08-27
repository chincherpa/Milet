using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Verkauf;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class BelegConfiguration : IEntityTypeConfiguration<Beleg>
{
    public void Configure(EntityTypeBuilder<Beleg> b)
    {
        b.ToTable("Belege", t => t.HasCheckConstraint(
            "CK_Belege_KundeOderLieferant",
            "([KundeId] IS NOT NULL AND [LieferantId] IS NULL) OR ([KundeId] IS NULL AND [LieferantId] IS NOT NULL)"));
        b.HasKey(x => x.Id);

        b.HasDiscriminator<string>("BelegTyp")
            .HasValue<Angebot>(nameof(BelegTyp.Angebot))
            .HasValue<Auftrag>(nameof(BelegTyp.Auftrag))
            .HasValue<Rechnung>(nameof(BelegTyp.Rechnung))
            .HasValue<Lieferschein>(nameof(BelegTyp.Lieferschein))
            .HasValue<Bestellung>(nameof(BelegTyp.Bestellung))
            .HasValue<Wareneingang>(nameof(BelegTyp.Wareneingang))
            .HasValue<Eingangsrechnung>(nameof(BelegTyp.Eingangsrechnung));

        b.Property(x => x.BelegNummer).HasMaxLength(20).IsRequired();
        b.HasIndex("BelegTyp", nameof(Beleg.BelegNummer))
            .IsUnique()
            .HasFilter("[BelegNummer] <> ''");

        // KundeId/LieferantId bewusst NICHT .IsRequired() — je nach Belegtyp ist genau eines von beiden gesetzt,
        // durchgesetzt vom CHECK-Constraint oben, nicht von EF-Required (das würde beide zwingend machen).
        b.HasOne(x => x.Kunde).WithMany().HasForeignKey(x => x.KundeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Lieferant).WithMany().HasForeignKey(x => x.LieferantId).OnDelete(DeleteBehavior.Restrict);

        b.OwnsOne(x => x.RechnungsadresseSnapshot, a =>
        {
            a.Property(p => p.Name1).HasColumnName("RgAdr_Name1").HasMaxLength(100).IsRequired();
            a.Property(p => p.Name2).HasColumnName("RgAdr_Name2").HasMaxLength(100);
            a.Property(p => p.Strasse).HasColumnName("RgAdr_Strasse").HasMaxLength(100);
            a.Property(p => p.Plz).HasColumnName("RgAdr_Plz").HasMaxLength(10);
            a.Property(p => p.Ort).HasColumnName("RgAdr_Ort").HasMaxLength(100);
            a.Property(p => p.Land).HasColumnName("RgAdr_Land").HasMaxLength(2);
        });
        b.Navigation(x => x.RechnungsadresseSnapshot).IsRequired();

        b.OwnsOne(x => x.LieferadresseSnapshot, a =>
        {
            a.Property(p => p.Name1).HasColumnName("LfAdr_Name1").HasMaxLength(100).IsRequired();
            a.Property(p => p.Name2).HasColumnName("LfAdr_Name2").HasMaxLength(100);
            a.Property(p => p.Strasse).HasColumnName("LfAdr_Strasse").HasMaxLength(100);
            a.Property(p => p.Plz).HasColumnName("LfAdr_Plz").HasMaxLength(10);
            a.Property(p => p.Ort).HasColumnName("LfAdr_Ort").HasMaxLength(100);
            a.Property(p => p.Land).HasColumnName("LfAdr_Land").HasMaxLength(2);
        });
        b.Navigation(x => x.LieferadresseSnapshot).IsRequired();

        b.Property(x => x.ZahlungsbedingungSkontoProzent).HasPrecision(5, 2);
        b.Property(x => x.SummeNetto).HasPrecision(18, 2);
        b.Property(x => x.SummeMwSt).HasPrecision(18, 2);
        b.Property(x => x.SummeBrutto).HasPrecision(18, 2);

        b.Property(x => x.Kopftext).HasMaxLength(2000);
        b.Property(x => x.Fusstext).HasMaxLength(2000);
        b.Property(x => x.ExterneReferenz).HasMaxLength(50);

        b.HasMany(x => x.Positionen).WithOne(p => p.Beleg).HasForeignKey(p => p.BelegId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Steuersummen).WithOne(s => s.Beleg).HasForeignKey(s => s.BelegId).OnDelete(DeleteBehavior.Cascade);

        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
