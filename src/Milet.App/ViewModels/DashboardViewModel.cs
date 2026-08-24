using CommunityToolkit.Mvvm.ComponentModel;

namespace Milet.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Begruessung { get; set; } = "Willkommen bei Milet Warenwirtschaft";
}
