using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Gaertnerei;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class SektionConfiguration : IEntityTypeConfiguration<Sektion>
{
    public void Configure(EntityTypeBuilder<Sektion> b)
    {
        b.ToTable("Sektionen");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(10).IsRequired();
        b.Property(x => x.Bezeichnung).HasMaxLength(100).IsRequired();
        b.Property(x => x.PosXMeter).HasPrecision(9, 2);
        b.Property(x => x.PosYMeter).HasPrecision(9, 2);
        b.Property(x => x.BreiteMeter).HasPrecision(9, 2);
        b.Property(x => x.HoeheMeter).HasPrecision(9, 2);
        b.Ignore(x => x.FlaecheQm);

        b.HasOne(x => x.Lagerort).WithMany().HasForeignKey(x => x.LagerortId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.LagerortId, x.Code }).IsUnique();

        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
