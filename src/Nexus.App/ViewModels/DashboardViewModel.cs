using CommunityToolkit.Mvvm.ComponentModel;

namespace Nexus.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Begruessung { get; set; } = "Willkommen bei Nexus Warenwirtschaft";
}
