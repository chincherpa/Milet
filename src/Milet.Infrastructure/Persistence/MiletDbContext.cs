using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Admin;
using Milet.Domain.Entities.Stammdaten;

namespace Milet.Infrastructure.Persistence;

public sealed class MiletDbContext(DbContextOptions<MiletDbContext> options) : DbContext(options)
{
    public DbSet<Einheit> Einheiten => Set<Einheit>();

    public DbSet<MwStSatz> MwStSaetze => Set<MwStSatz>();

    public DbSet<Zahlungsbedingung> Zahlungsbedingungen => Set<Zahlungsbedingung>();

    public DbSet<Versandart> Versandarten => Set<Versandart>();

    public DbSet<Kunde> Kunden => Set<Kunde>();

    public DbSet<Lieferant> Lieferanten => Set<Lieferant>();

    public DbSet<Artikel> Artikel => Set<Artikel>();

    public DbSet<Preisliste> Preislisten => Set<Preisliste>();

    public DbSet<ArtikelPreis> ArtikelPreise => Set<ArtikelPreis>();

    public DbSet<Nummernkreis> Nummernkreise => Set<Nummernkreis>();

    public DbSet<Milet.Domain.Entities.Verkauf.Beleg> Belege => Set<Milet.Domain.Entities.Verkauf.Beleg>();
    public DbSet<Milet.Domain.Entities.Verkauf.Angebot> Angebote => Set<Milet.Domain.Entities.Verkauf.Angebot>();
    public DbSet<Milet.Domain.Entities.Verkauf.Auftrag> Auftraege => Set<Milet.Domain.Entities.Verkauf.Auftrag>();
    public DbSet<Milet.Domain.Entities.Verkauf.Rechnung> Rechnungen => Set<Milet.Domain.Entities.Verkauf.Rechnung>();
    public DbSet<Milet.Domain.Entities.Verkauf.BelegPosition> BelegPositionen => Set<Milet.Domain.Entities.Verkauf.BelegPosition>();
    public DbSet<Milet.Domain.Entities.Verkauf.BelegSteuerSumme> BelegSteuerSummen => Set<Milet.Domain.Entities.Verkauf.BelegSteuerSumme>();
    public DbSet<Milet.Domain.Entities.Finanzen.OffenerPosten> OffenePosten => Set<Milet.Domain.Entities.Finanzen.OffenerPosten>();
    public DbSet<Firmenstamm> Firmenstamm => Set<Firmenstamm>();
    public DbSet<Milet.Domain.Entities.Verkauf.Lieferschein> Lieferscheine => Set<Milet.Domain.Entities.Verkauf.Lieferschein>();
    public DbSet<Milet.Domain.Entities.Verkauf.Bestellung> Bestellungen => Set<Milet.Domain.Entities.Verkauf.Bestellung>();
    public DbSet<Milet.Domain.Entities.Verkauf.Wareneingang> Wareneingaenge => Set<Milet.Domain.Entities.Verkauf.Wareneingang>();
    public DbSet<Milet.Domain.Entities.Verkauf.Eingangsrechnung> Eingangsrechnungen => Set<Milet.Domain.Entities.Verkauf.Eingangsrechnung>();
    public DbSet<Milet.Domain.Entities.Lager.Lagerort> Lagerorte => Set<Milet.Domain.Entities.Lager.Lagerort>();
    public DbSet<Milet.Domain.Entities.Lager.Lagerbewegung> Lagerbewegungen => Set<Milet.Domain.Entities.Lager.Lagerbewegung>();
    public DbSet<Milet.Domain.Entities.Lager.ArtikelBestand> ArtikelBestaende => Set<Milet.Domain.Entities.Lager.ArtikelBestand>();
    public DbSet<Milet.Domain.Entities.Lager.Seriennummer> Seriennummern => Set<Milet.Domain.Entities.Lager.Seriennummer>();
    public DbSet<Milet.Domain.Entities.Lager.BelegPositionSeriennummer> BelegPositionSeriennummern => Set<Milet.Domain.Entities.Lager.BelegPositionSeriennummer>();
    public DbSet<Milet.Domain.Entities.Lager.Inventur> Inventuren => Set<Milet.Domain.Entities.Lager.Inventur>();
    public DbSet<Milet.Domain.Entities.Lager.InventurPosition> InventurPositionen => Set<Milet.Domain.Entities.Lager.InventurPosition>();
    public DbSet<Milet.Domain.Entities.Finanzen.Zahlung> Zahlungen => Set<Milet.Domain.Entities.Finanzen.Zahlung>();
    public DbSet<Milet.Domain.Entities.Finanzen.ZahlungZuordnung> ZahlungZuordnungen => Set<Milet.Domain.Entities.Finanzen.ZahlungZuordnung>();
    public DbSet<Milet.Domain.Entities.Finanzen.Mahnstufe> Mahnstufen => Set<Milet.Domain.Entities.Finanzen.Mahnstufe>();
    public DbSet<Milet.Domain.Entities.Finanzen.Mahnung> Mahnungen => Set<Milet.Domain.Entities.Finanzen.Mahnung>();
    public DbSet<Milet.Domain.Entities.Finanzen.MahnungPosition> MahnungPositionen => Set<Milet.Domain.Entities.Finanzen.MahnungPosition>();
    public DbSet<Milet.Domain.Entities.Finanzen.EmailVersand> EmailVersand => Set<Milet.Domain.Entities.Finanzen.EmailVersand>();
    public DbSet<FibuKonfiguration> FibuKonfiguration => Set<FibuKonfiguration>();
    public DbSet<Recht> Rechte => Set<Recht>();
    public DbSet<Rolle> Rollen => Set<Rolle>();
    public DbSet<Benutzer> Benutzer => Set<Benutzer>();
    public DbSet<AuditLog> AuditLog => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MiletDbContext).Assembly);
    }
}
