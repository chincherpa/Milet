using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Milet.App.ViewModels.Verkauf;

namespace Milet.App.Views.Verkauf;

public sealed partial class AuftragListPage : Page
{
    public AuftragListViewModel ViewModel { get; }
    public AuftragListPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<AuftragListViewModel>();
        InitializeComponent();
    }
}
