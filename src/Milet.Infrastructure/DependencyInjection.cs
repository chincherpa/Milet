using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Einkauf;
using Milet.Application.Finanzen;
using Milet.Application.Gaertnerei;
using Milet.Application.Lager;
using Milet.Application.Reporting;
using Milet.Application.Stammdaten;
using Milet.Application.Verkauf;
using Milet.Infrastructure.Email;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Persistence.Interceptors;
using Milet.Infrastructure.Services;

namespace Milet.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Milet")
            ?? throw new InvalidOperationException("ConnectionStrings:Milet fehlt in der Konfiguration.");

        services.AddSingleton<CurrentSessionService>();
        services.AddSingleton<ICurrentUserService>(sp => sp.GetRequiredService<CurrentSessionService>());
        services.AddSingleton<ICurrentSessionService>(sp => sp.GetRequiredService<CurrentSessionService>());
        services.AddSingleton<IBerechtigungsService, BerechtigungsService>();
        services.AddSingleton<AuditSaveChangesInterceptor>();
        services.AddSingleton<BelegImmutabilityInterceptor>();

        services.AddDbContextFactory<MiletDbContext>((sp, options) =>
            options.UseSqlServer(connectionString)
                .AddInterceptors(
                    sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                    sp.GetRequiredService<BelegImmutabilityInterceptor>()));

        // Transient statt Scoped: die Services sind zustandslos und ziehen je Aufruf einen eigenen DbContext
        // aus der Factory. Die WinUI-App löst sie über App.Host.Services (Root-Scope) auf — als "Scoped"
        // registriert lebten sie faktisch bis zum App-Ende und die Registrierung versprach eine
        // Lebensdauerbegrenzung, die es nicht gab.
        services.AddTransient<INumberRangeService, NumberRangeService>();

        services.AddTransient<IKundenService, KundenService>();
        services.AddTransient<ILieferantenService, LieferantenService>();
        services.AddTransient<IArtikelService, ArtikelService>();
        services.AddTransient<IStammdatenLookupService, StammdatenLookupService>();

        services.AddTransient<IEinheitenService, EinheitenService>();
        services.AddTransient<IMwStSaetzeService, MwStSaetzeService>();
        services.AddTransient<IZahlungsbedingungenService, ZahlungsbedingungenService>();
        services.AddTransient<IVersandartenService, VersandartenService>();
        services.AddTransient<IPreislistenService, PreislistenService>();
        services.AddTransient<IArtikelPreiseService, ArtikelPreiseService>();

        services.AddTransient<IBelegService, BelegService>();
        services.AddTransient<IVerkaufLookupService, VerkaufLookupService>();
        services.AddTransient<IFirmenstammService, FirmenstammService>();
        services.AddTransient<IBelegUeberleitungService, BelegUeberleitungService>();
        services.AddTransient<IRechnungBuchenService, RechnungBuchenService>();
        services.AddTransient<IPdfService, Pdf.PdfService>();
        services.AddTransient<IBestandService, BestandService>();
        services.AddTransient<ILagerortService, LagerortService>();
        services.AddTransient<ISeriennummernService, SeriennummernService>();
        services.AddTransient<ILieferscheinBuchenService, LieferscheinBuchenService>();
        services.AddTransient<IInventurService, InventurService>();
        services.AddTransient<IKulturstufenService, KulturstufenService>();
        services.AddTransient<IGaertnereiplanService, GaertnereiplanService>();
        services.AddTransient<IKulturBuchungService, KulturBuchungService>();
        services.AddTransient<IKulturBestandService, KulturBestandService>();

        services.AddTransient<IEinkaufLookupService, EinkaufLookupService>();
        services.AddTransient<IBestellVorschlagService, BestellVorschlagService>();
        services.AddTransient<IWareneingangBuchenService, WareneingangBuchenService>();
        services.AddTransient<IEingangsrechnungBuchenService, EingangsrechnungBuchenService>();

        services.AddTransient<IOffenePostenService, OffenePostenService>();
        services.AddTransient<IZahlungService, ZahlungService>();
        services.AddTransient<IMahnwesenService, MahnwesenService>();

        services.AddTransient<IFibuKonfigurationService, FibuKonfigurationService>();
        services.AddTransient<IDatevExportService, DatevExportService>();
        services.AddTransient<IReportingService, ReportingService>();

        services.AddTransient<IAuthService, AuthService>();
        services.AddTransient<IBenutzerverwaltungService, BenutzerverwaltungService>();
        services.AddTransient<IRollenverwaltungService, RollenverwaltungService>();
        services.AddTransient<IAuditLogService, AuditLogService>();
        services.AddTransient<ISchemaVersionService, SchemaVersionService>();

        // E-Mail-Versand: nur registriert, wenn appsettings.json eine vollständige "Graph"-Sektion trägt —
        // sonst NichtKonfigurierterEmailService (wirft beim Versandversuch eine sprechende Exception,
        // blockiert aber nie Buchen/PDF/Drucken). Milet.App überschreibt IWindowHandleProvider mit der
        // echten WinUI-Fensterimplementierung (Registrierung nach AddInfrastructure gewinnt).
        services.AddSingleton<IWindowHandleProvider, NullWindowHandleProvider>();

        var graphSection = configuration.GetSection(GraphSettings.SectionName);
        var graphSettings = graphSection.Get<GraphSettings>();
        if (graphSection.Exists() && graphSettings is { ClientId.Length: > 0, TenantId.Length: > 0, RedirectUri.Length: > 0 })
        {
            services.AddSingleton(sp => PublicClientApplicationBuilder.Create(graphSettings.ClientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, graphSettings.TenantId)
                .WithRedirectUri(graphSettings.RedirectUri)
                .WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows))
                .Build());
            services.AddTransient<IEmailService, GraphEmailService>();
        }
        else
        {
            services.AddTransient<IEmailService, NichtKonfigurierterEmailService>();
        }

        services.AddTransient<IEmailVersandService, EmailVersandService>();

        return services;
    }
}
