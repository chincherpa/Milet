using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Reporting;

namespace Milet.App.Views.Reporting;

public sealed partial class ReportingPage : Page
{
    public ReportingViewModel ViewModel { get; }

    public ReportingPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<ReportingViewModel>();
        InitializeComponent();
    }
}
