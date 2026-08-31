using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Lager;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class LagerbewegungConfiguration : IEntityTypeConfiguration<Lagerbewegung>
{
    public void Configure(EntityTypeBuilder<Lagerbewegung> b)
    {
        b.ToTable("Lagerbewegungen");
        b.HasKey(x => x.Id);
        b.Property(x => x.Menge).HasPrecision(18, 3);
        // 300 statt 200: automatische Storno-Bemerkungen kombinieren einen Präfix (z. B. "Storno Lieferschein
        // LS-2026-0001: ") mit dem bis zu 200 Zeichen langen, vom Nutzer erfassten Storno-Grund.
        b.Property(x => x.Bemerkung).HasMaxLength(300);

        b.HasOne(x => x.Artikel).WithMany().HasForeignKey(x => x.ArtikelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Lagerort).WithMany().HasForeignKey(x => x.LagerortId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Sektion).WithMany().HasForeignKey(x => x.SektionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Kulturstufe).WithMany().HasForeignKey(x => x.KulturstufeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.BelegPosition).WithMany().HasForeignKey(x => x.BelegPositionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Seriennummer).WithMany().HasForeignKey(x => x.SeriennummerId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.ArtikelId, x.LagerortId, x.SektionId, x.KulturstufeId });
    }
}
