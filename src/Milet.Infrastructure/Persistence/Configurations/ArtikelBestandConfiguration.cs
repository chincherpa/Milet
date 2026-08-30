using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Lager;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class ArtikelBestandConfiguration : IEntityTypeConfiguration<ArtikelBestand>
{
    public void Configure(EntityTypeBuilder<ArtikelBestand> b)
    {
        b.ToTable("ArtikelBestaende");
        b.HasKey(x => x.Id);
        b.Property(x => x.Menge).HasPrecision(18, 3);

        b.HasOne(x => x.Artikel).WithMany().HasForeignKey(x => x.ArtikelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Lagerort).WithMany().HasForeignKey(x => x.LagerortId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Sektion).WithMany().HasForeignKey(x => x.SektionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Kulturstufe).WithMany().HasForeignKey(x => x.KulturstufeId).OnDelete(DeleteBehavior.Restrict);

        // E3: SektionId/KulturstufeId sind NULL bei Handelsware. Anders als der ANSI-Standard (und Postgres)
        // behandelt SQL Server NULL in einem Unique-Index als GLEICH — ein zweiter (ArtikelId, LagerortId,
        // NULL, NULL)-Insert wird vom Index abgelehnt. Die heutige Eindeutigkeit "ein Artikel, ein Lagerort"
        // für Handelsware bleibt damit unverändert bestehen, ohne gefilterten Zusatzindex. Wer diesen Index
        // später "aufräumt" (z. B. durch einen gefilterten Index ersetzt), bricht diese Garantie stillschweigend
        // — nicht anfassen, ohne den Integrationstest aus Task 6 (zweiter Insert mit identischen NULLs schlägt fehl) erneut laufen zu lassen.
        // .HasFilter(null) ist Pflicht: ohne sie legt die EF-Core-SqlServer-Konvention automatisch einen
        // gefilterten Index "WHERE SektionId IS NOT NULL AND KulturstufeId IS NOT NULL" an (ANSI-Semantik,
        // NULL != NULL) und hebelt damit genau die oben beschriebene, gewollte SQL-Server-Eigenheit aus.
        b.HasIndex(x => new { x.ArtikelId, x.LagerortId, x.SektionId, x.KulturstufeId }).IsUnique().HasFilter(null);

        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
