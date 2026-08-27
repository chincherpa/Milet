using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Milet.App.Services;
using Milet.App.ViewModels;
using Milet.App.ViewModels.Admin;
using Milet.App.ViewModels.Einkauf;
using Milet.App.ViewModels.Finanzen;
using Milet.App.ViewModels.Lager;
using Milet.App.ViewModels.Reporting;
using Milet.App.ViewModels.Stammdaten;
using Milet.App.ViewModels.Verkauf;
using Milet.App.Views;
using Milet.Infrastructure;
using Serilog;

namespace Milet.App;

public partial class App : Microsoft.UI.Xaml.Application
{
    public static IHost Host { get; private set; } = null!;

    /// <summary>Erst nach erfolgreichem Login gesetzt (s. LoginWindow) — DialogService und
    /// andere Seiten dürfen erst danach darauf zugreifen.</summary>
    public static Window MainWindow { get; internal set; } = null!;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Host = BuildHost();
        Host.Start();

        var loginWindow = new LoginWindow();
        loginWindow.Activate();
    }

    private static IHost BuildHost()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false);

        builder.Services.AddSerilog(logger => logger
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(AppContext.BaseDirectory, "logs", "milet-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14));

        builder.Services.AddInfrastructure(builder.Configuration);

        // Überschreibt den NullWindowHandleProvider-Fallback aus AddInfrastructure — DI löst bei mehreren
        // Registrierungen desselben Diensts die zuletzt registrierte auf.
        builder.Services.AddSingleton<Milet.Application.Abstractions.IWindowHandleProvider, WinUiWindowHandleProvider>();

        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<IDialogService, DialogService>();

        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<KundenListViewModel>();
        builder.Services.AddTransient<KundeEditViewModel>();
        builder.Services.AddTransient<LieferantenListViewModel>();
        builder.Services.AddTransient<LieferantEditViewModel>();
        builder.Services.AddTransient<ArtikelListViewModel>();
        builder.Services.AddTransient<ArtikelEditViewModel>();
        builder.Services.AddTransient<KleinstammViewModel>();

        builder.Services.AddTransient<AngebotListViewModel>();
        builder.Services.AddTransient<AuftragListViewModel>();
        builder.Services.AddTransient<RechnungListViewModel>();
        builder.Services.AddTransient<AngebotEditViewModel>();
        builder.Services.AddTransient<AuftragEditViewModel>();
        builder.Services.AddTransient<RechnungEditViewModel>();

        builder.Services.AddTransient<BestandUebersichtViewModel>();
        builder.Services.AddTransient<LieferscheinListViewModel>();
        builder.Services.AddTransient<LieferscheinEditViewModel>();
        builder.Services.AddTransient<InventurListViewModel>();
        builder.Services.AddTransient<InventurEditViewModel>();

        builder.Services.AddTransient<BestellVorschlagViewModel>();
        builder.Services.AddTransient<BestellungListViewModel>();
        builder.Services.AddTransient<BestellungEditViewModel>();
        builder.Services.AddTransient<WareneingangListViewModel>();
        builder.Services.AddTransient<WareneingangEditViewModel>();
        builder.Services.AddTransient<EingangsrechnungListViewModel>();
        builder.Services.AddTransient<EingangsrechnungEditViewModel>();

        builder.Services.AddTransient<OffenePostenListViewModel>();
        builder.Services.AddTransient<MahnlaufViewModel>();
        builder.Services.AddTransient<DatevExportViewModel>();

        builder.Services.AddTransient<ReportingViewModel>();

        builder.Services.AddTransient<AdministrationViewModel>();

        return builder.Build();
    }
}
