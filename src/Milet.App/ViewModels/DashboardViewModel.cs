using CommunityToolkit.Mvvm.ComponentModel;
using Milet.Application.Abstractions;

namespace Milet.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    public DashboardViewModel(ICurrentSessionService session)
    {
        Begruessung = $"Willkommen bei Milet Warenwirtschaft, {session.BenutzerName} ({session.RollenName})";
    }

    [ObservableProperty]
    public partial string Begruessung { get; set; } = "Willkommen bei Milet Warenwirtschaft";
}
