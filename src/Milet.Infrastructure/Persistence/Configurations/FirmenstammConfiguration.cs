using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Admin;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class FirmenstammConfiguration : IEntityTypeConfiguration<Firmenstamm>
{
    public void Configure(EntityTypeBuilder<Firmenstamm> b)
    {
        b.ToTable("Firmenstamm");
        b.HasKey(x => x.Id);
        // Singleton-Zeile (immer Id = 1) — keine Identity-Spalte, der Aufrufer setzt die Id explizit.
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Firmenname).HasMaxLength(100).IsRequired();
        b.OwnsOne(x => x.Adresse, a =>
        {
            a.Property(p => p.Name1).HasColumnName("Name1").HasMaxLength(100).IsRequired();
            a.Property(p => p.Name2).HasColumnName("Name2").HasMaxLength(100);
            a.Property(p => p.Strasse).HasColumnName("Strasse").HasMaxLength(100);
            a.Property(p => p.Plz).HasColumnName("Plz").HasMaxLength(10);
            a.Property(p => p.Ort).HasColumnName("Ort").HasMaxLength(100);
            a.Property(p => p.Land).HasColumnName("Land").HasMaxLength(2);
        });
        b.Navigation(x => x.Adresse).IsRequired();
    }
}
