using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Lager;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class LagerortConfiguration : IEntityTypeConfiguration<Lagerort>
{
    public void Configure(EntityTypeBuilder<Lagerort> b)
    {
        b.ToTable("Lagerorte");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(10).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Bezeichnung).HasMaxLength(100).IsRequired();

        b.Property(x => x.PosXMeter).HasPrecision(9, 2);
        b.Property(x => x.PosYMeter).HasPrecision(9, 2);
        b.Property(x => x.BreiteMeter).HasPrecision(9, 2);
        b.Property(x => x.HoeheMeter).HasPrecision(9, 2);
        b.HasOne(x => x.Gaertnereiplan).WithMany().HasForeignKey(x => x.GaertnereiplanId).OnDelete(DeleteBehavior.Restrict);

        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
