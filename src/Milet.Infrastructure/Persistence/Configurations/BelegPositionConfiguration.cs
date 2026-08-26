using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Lager;
using Milet.Domain.Entities.Stammdaten;
using Milet.Domain.Entities.Verkauf;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class BelegPositionConfiguration : IEntityTypeConfiguration<BelegPosition>
{
    public void Configure(EntityTypeBuilder<BelegPosition> b)
    {
        b.ToTable("BelegPositionen");
        b.HasKey(x => x.Id);

        b.Property(x => x.Bezeichnung).HasMaxLength(200).IsRequired();
        b.Property(x => x.EinheitKuerzel).HasMaxLength(10);

        b.Property(x => x.Menge).HasPrecision(18, 3);
        b.Property(x => x.Einzelpreis).HasPrecision(18, 4);
        b.Property(x => x.RabattProzent).HasPrecision(5, 2);
        b.Property(x => x.MwStSatzWert).HasPrecision(5, 2);
        b.Property(x => x.GesamtNetto).HasPrecision(18, 2);

        b.HasOne(x => x.Artikel).WithMany().HasForeignKey(x => x.ArtikelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MwStSatz>().WithMany().HasForeignKey(x => x.MwStSatzId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Lagerort).WithMany().HasForeignKey(x => x.LagerortId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne<BelegPosition>().WithMany()
            .HasForeignKey(x => x.UrsprungsPositionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
