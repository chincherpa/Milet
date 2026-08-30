using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Gaertnerei;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class GaertnereiplanConfiguration : IEntityTypeConfiguration<Gaertnereiplan>
{
    public void Configure(EntityTypeBuilder<Gaertnereiplan> b)
    {
        b.ToTable("Gaertnereiplaene");
        b.HasKey(x => x.Id);
        b.Property(x => x.Bezeichnung).HasMaxLength(100).IsRequired();
        b.Property(x => x.BreiteMeter).HasPrecision(9, 2);
        b.Property(x => x.HoeheMeter).HasPrecision(9, 2);
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
