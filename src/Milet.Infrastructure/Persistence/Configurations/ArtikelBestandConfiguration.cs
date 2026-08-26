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
        b.HasIndex(x => new { x.ArtikelId, x.LagerortId }).IsUnique();

        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
