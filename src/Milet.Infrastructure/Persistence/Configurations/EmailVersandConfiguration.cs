using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Finanzen;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class EmailVersandConfiguration : IEntityTypeConfiguration<EmailVersand>
{
    public void Configure(EntityTypeBuilder<EmailVersand> b)
    {
        b.ToTable("EmailVersand", t => t.HasCheckConstraint(
            "CK_EmailVersand_BelegOderMahnung",
            "([BelegId] IS NOT NULL AND [MahnungId] IS NULL) OR ([BelegId] IS NULL AND [MahnungId] IS NOT NULL)"));
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Beleg).WithMany().HasForeignKey(x => x.BelegId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Mahnung).WithMany().HasForeignKey(x => x.MahnungId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Empfaenger).HasMaxLength(320).IsRequired();
        b.Property(x => x.Betreff).HasMaxLength(200).IsRequired();
        b.Property(x => x.Fehlermeldung).HasMaxLength(2000);
    }
}
