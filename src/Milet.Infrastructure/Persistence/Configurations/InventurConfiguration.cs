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
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
