using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Nexus.App.Services;
using Nexus.App.ViewModels;
using Nexus.App.ViewModels.Stammdaten;
using Nexus.App.Views;
using Nexus.Infrastructure;
using Serilog;

namespace Nexus.App;

public partial class App : Microsoft.UI.Xaml.Application
{
    public static IHost Host { get; private set; } = null!;
    public static Window MainWindow { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Host = BuildHost();
        Host.Start();

        MainWindow = new MainWindow();
        MainWindow.Activate();
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
                Path.Combine(AppContext.BaseDirectory, "logs", "nexus-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14));

        builder.Services.AddInfrastructure(builder.Configuration);

        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<IDialogService, DialogService>();

        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<KundenListViewModel>();
        builder.Services.AddTransient<KundeEditViewModel>();
        builder.Services.AddTransient<LieferantenListViewModel>();
        builder.Services.AddTransient<LieferantEditViewModel>();
        builder.Services.AddTransient<ArtikelListViewModel>();
        builder.Services.AddTransient<ArtikelEditViewModel>();

        return builder.Build();
    }
}
