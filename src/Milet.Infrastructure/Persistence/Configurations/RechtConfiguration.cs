using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Admin;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class RechtConfiguration : IEntityTypeConfiguration<Recht>
{
    public void Configure(EntityTypeBuilder<Recht> b)
    {
        b.ToTable("Rechte");
        b.HasKey(r => r.Id);
        b.Property(r => r.Code).HasMaxLength(50).IsRequired();
        b.HasIndex(r => r.Code).IsUnique();
        b.Property(r => r.Bezeichnung).HasMaxLength(100).IsRequired();
    }
}

public sealed class RolleConfiguration : IEntityTypeConfiguration<Rolle>
{
    public void Configure(EntityTypeBuilder<Rolle> b)
    {
        b.ToTable("Rollen");
        b.HasKey(r => r.Id);
        b.Property(r => r.Name).HasMaxLength(50).IsRequired();
        b.HasIndex(r => r.Name).IsUnique();
        b.Property(r => r.Beschreibung).HasMaxLength(200);
        b.Property(r => r.RowVersion).IsRowVersion();

        b.HasMany(r => r.Rechte)
            .WithMany(r => r.Rollen)
            .UsingEntity(j => j.ToTable("RolleRecht"));
    }
}

public sealed class BenutzerConfiguration : IEntityTypeConfiguration<Benutzer>
{
    public void Configure(EntityTypeBuilder<Benutzer> b)
    {
        b.ToTable("Benutzer");
        b.HasKey(x => x.Id);
        b.Property(x => x.Benutzername).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Benutzername).IsUnique();
        b.Property(x => x.Anzeigename).HasMaxLength(100).IsRequired();
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.PasswortHash).HasMaxLength(200).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.Rolle)
            .WithMany(r => r.Benutzer)
            .HasForeignKey(x => x.RolleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLog");
        b.HasKey(x => x.Id);
        b.Property(x => x.BenutzerName).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityName).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityId).HasMaxLength(50).IsRequired();
        b.Property(x => x.Aktion).HasMaxLength(20).IsRequired();
        b.Property(x => x.Aenderungen).HasColumnType("nvarchar(max)");
        b.HasIndex(x => x.Zeitpunkt);
        b.HasIndex(x => new { x.EntityName, x.EntityId });
    }
}
