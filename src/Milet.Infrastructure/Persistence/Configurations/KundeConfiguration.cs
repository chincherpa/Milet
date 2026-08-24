using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Domain.Entities.Stammdaten;

namespace Nexus.Infrastructure.Persistence.Configurations;

public sealed class KundeConfiguration : IEntityTypeConfiguration<Kunde>
{
    public void Configure(EntityTypeBuilder<Kunde> b)
    {
        b.ToTable("Kunden");
        b.HasKey(k => k.Id);
        b.Property(k => k.Kundennummer).HasMaxLength(20).IsRequired();
        b.HasIndex(k => k.Kundennummer).IsUnique();

        b.OwnsOne(k => k.Adresse, a =>
        {
            a.Property(x => x.Name1).HasColumnName("Name1").HasMaxLength(100).IsRequired();
            a.Property(x => x.Name2).HasColumnName("Name2").HasMaxLength(100);
            a.Property(x => x.Strasse).HasColumnName("Strasse").HasMaxLength(100);
            a.Property(x => x.Plz).HasColumnName("Plz").HasMaxLength(10);
            a.Property(x => x.Ort).HasColumnName("Ort").HasMaxLength(100);
            a.Property(x => x.Land).HasColumnName("Land").HasMaxLength(2);
        });
        b.Navigation(k => k.Adresse).IsRequired();

        b.Property(k => k.RabattProzent).HasPrecision(5, 2);
        b.Property(k => k.Kreditlimit).HasPrecision(18, 2);

        b.HasOne(k => k.Zahlungsbedingung)
            .WithMany()
            .HasForeignKey(k => k.ZahlungsbedingungId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(k => k.Preisliste)
            .WithMany()
            .HasForeignKey(k => k.PreislisteId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Property(k => k.RowVersion).IsRowVersion();
    }
}
