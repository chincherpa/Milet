using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Finanzen;

namespace Milet.App.Views.Finanzen;

public sealed partial class DatevExportPage : Page
{
    public DatevExportViewModel ViewModel { get; }

    public DatevExportPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<DatevExportViewModel>();
        InitializeComponent();
    }
}
