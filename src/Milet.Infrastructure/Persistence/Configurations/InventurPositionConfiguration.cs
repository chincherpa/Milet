using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Lager;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class InventurPositionConfiguration : IEntityTypeConfiguration<InventurPosition>
{
    public void Configure(EntityTypeBuilder<InventurPosition> b)
    {
        b.ToTable("InventurPositionen");
        b.HasKey(x => x.Id);
        b.Property(x => x.SollMenge).HasPrecision(18, 3);
        b.Property(x => x.IstMenge).HasPrecision(18, 3);
        b.HasOne(x => x.Artikel).WithMany().HasForeignKey(x => x.ArtikelId).OnDelete(DeleteBehavior.Restrict);
    }
}
