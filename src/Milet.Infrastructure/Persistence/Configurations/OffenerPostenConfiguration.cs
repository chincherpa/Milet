using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Finanzen;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class OffenerPostenConfiguration : IEntityTypeConfiguration<OffenerPosten>
{
    public void Configure(EntityTypeBuilder<OffenerPosten> b)
    {
        b.ToTable("OffenePosten");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.BelegId).IsUnique();
        b.HasOne(x => x.Beleg).WithMany().HasForeignKey(x => x.BelegId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Betrag).HasPrecision(18, 2);
        b.Property(x => x.OffenerBetrag).HasPrecision(18, 2);
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
