using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Admin;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class FibuKonfigurationConfiguration : IEntityTypeConfiguration<FibuKonfiguration>
{
    public void Configure(EntityTypeBuilder<FibuKonfiguration> b)
    {
        b.ToTable("FibuKonfiguration");
        b.HasKey(x => x.Id);
        // Singleton-Zeile (immer Id = 1) — keine Identity-Spalte, der Aufrufer setzt die Id explizit
        // (analog FirmenstammConfiguration).
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Kontenrahmen).HasConversion<string>().HasMaxLength(10);
    }
}
