using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Domain.Entities.Stammdaten;

namespace Nexus.Infrastructure.Persistence.Configurations;

public sealed class LieferantConfiguration : IEntityTypeConfiguration<Lieferant>
{
    public void Configure(EntityTypeBuilder<Lieferant> b)
    {
        b.ToTable("Lieferanten");
        b.HasKey(l => l.Id);
        b.Property(l => l.Lieferantennummer).HasMaxLength(20).IsRequired();
        b.HasIndex(l => l.Lieferantennummer).IsUnique();

        b.OwnsOne(l => l.Adresse, a =>
        {
            a.Property(x => x.Name1).HasColumnName("Name1").HasMaxLength(100).IsRequired();
            a.Property(x => x.Name2).HasColumnName("Name2").HasMaxLength(100);
            a.Property(x => x.Strasse).HasColumnName("Strasse").HasMaxLength(100);
            a.Property(x => x.Plz).HasColumnName("Plz").HasMaxLength(10);
            a.Property(x => x.Ort).HasColumnName("Ort").HasMaxLength(100);
            a.Property(x => x.Land).HasColumnName("Land").HasMaxLength(2);
        });
        b.Navigation(l => l.Adresse).IsRequired();

        b.HasOne(l => l.Zahlungsbedingung)
            .WithMany()
            .HasForeignKey(l => l.ZahlungsbedingungId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Property(l => l.RowVersion).IsRowVersion();
    }
}
