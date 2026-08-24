using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Admin;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class NummernkreisConfiguration : IEntityTypeConfiguration<Nummernkreis>
{
    public void Configure(EntityTypeBuilder<Nummernkreis> b)
    {
        b.ToTable("Nummernkreise");
        b.HasKey(n => n.Id);
        b.Property(n => n.Code).HasMaxLength(10).IsRequired();
        b.Property(n => n.Format).HasMaxLength(30).IsRequired();
        b.HasIndex(n => new { n.Code, n.Jahr }).IsUnique();
    }
}
