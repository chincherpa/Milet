using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Lager;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class BelegPositionSeriennummerConfiguration : IEntityTypeConfiguration<BelegPositionSeriennummer>
{
    public void Configure(EntityTypeBuilder<BelegPositionSeriennummer> b)
    {
        b.ToTable("BelegPositionSeriennummern");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.BelegPosition).WithMany().HasForeignKey(x => x.BelegPositionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Seriennummer).WithMany().HasForeignKey(x => x.SeriennummerId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.BelegPositionId, x.SeriennummerId }).IsUnique();
    }
}
