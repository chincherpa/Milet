using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Lager;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class InventurConfiguration : IEntityTypeConfiguration<Inventur>
{
    public void Configure(EntityTypeBuilder<Inventur> b)
    {
        b.ToTable("Inventuren");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Lagerort).WithMany().HasForeignKey(x => x.LagerortId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Positionen).WithOne(p => p.Inventur).HasForeignKey(p => p.InventurId).OnDelete(DeleteBehavior.Cascade);
        // Macht die Regel "höchstens eine offene Inventur je Lagerort" unumgehbar. Der Guard in
        // InventurService.NeueInventurAsync war ein Read-then-Insert ohne Sperre: unter SQL Servers Default
        // READ COMMITTED sehen zwei parallele Aufrufer beide "keine offene Inventur" und legen beide eine an.
        // Genau diese Regel trägt aber den Fix gegen die Inventur-Doppelzählung (zwei offene Inventuren
        // korrigieren nacheinander gegen denselben eingefrorenen Sollstand) — sie gehört deshalb in die
        // Datenbank, nicht nur in die Anwendungsschicht.
        // Der Filter ist auf den Literalwert von InventurStatus.Offen (= 0) geschrieben, weil SQL Server im
        // Indexfilter keine Parameter erlaubt; abgeschlossene Inventuren sind bewusst nicht erfasst, davon
        // darf es je Lagerort beliebig viele geben.
        b.HasIndex(x => x.LagerortId).IsUnique().HasFilter("[Status] = 0");

        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
