using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Stammdaten;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class ArtikelConfiguration : IEntityTypeConfiguration<Artikel>
{
    public void Configure(EntityTypeBuilder<Artikel> b)
    {
        b.ToTable("Artikel");
        b.HasKey(a => a.Id);
        b.Property(a => a.Artikelnummer).HasMaxLength(30).IsRequired();
        b.HasIndex(a => a.Artikelnummer).IsUnique();
        b.Property(a => a.Bezeichnung).HasMaxLength(200).IsRequired();
        b.Property(a => a.Ean).HasMaxLength(20);

        b.Property(a => a.Einkaufspreis).HasPrecision(18, 4);
        b.Property(a => a.Listenpreis).HasPrecision(18, 4);
        b.Property(a => a.Gewicht).HasPrecision(18, 3);
        b.Property(a => a.Mindestbestand).HasPrecision(18, 3);

        b.HasOne(a => a.Einheit).WithMany().HasForeignKey(a => a.EinheitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(a => a.MwStSatz).WithMany().HasForeignKey(a => a.MwStSatzId).OnDelete(DeleteBehavior.Restrict);

        b.Property(a => a.RowVersion).IsRowVersion();
    }
}
