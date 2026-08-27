using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Milet.App.Services;
using Milet.Application.Finanzen;
using Milet.Domain.Entities.Finanzen;

namespace Milet.App.ViewModels.Finanzen;

public sealed partial class OffenePostenListViewModel : ObservableObject
{
    private readonly IOffenePostenService _offenePostenService;
    private readonly IDialogService _dialogService;

    public OffenePostenListViewModel(IOffenePostenService offenePostenService, IDialogService dialogService)
    {
        _offenePostenService = offenePostenService;
        _dialogService = dialogService;
        _ = LadenAsync();
    }

    [ObservableProperty] public partial IReadOnlyList<OffenePostenDto> Posten { get; set; } = [];
    [ObservableProperty] public partial bool LaedtGerade { get; set; }

    /// <summary>0 = Alle, 1 = Debitor, 2 = Kreditor.</summary>
    [ObservableProperty] public partial int TypFilterIndex { get; set; }

    /// <summary>0 = Alle, 1 = Offen, 2 = TeilweiseBezahlt, 3 = Ausgeglichen.</summary>
    [ObservableProperty] public partial int StatusFilterIndex { get; set; }

    [ObservableProperty] public partial bool NurUeberfaellige { get; set; }

    /// <summary>Wird per Code-Behind aus `ListView.SelectionChanged` befüllt (Mehrfachauswahl für eine
    /// Sammelzahlung über mehrere offene Posten desselben Partners) — siehe OffenePostenListPage.xaml.cs.</summary>
    public List<OffenePostenDto> AusgewaehltePosten { get; set; } = [];

    [RelayCommand]
    private async Task LadenAsync()
    {
        LaedtGerade = true;
        try
        {
            var filter = new OffenePostenFilterDto(
                Typ: TypFilterIndex switch { 1 => OffenerPostenTyp.Debitor, 2 => OffenerPostenTyp.Kreditor, _ => null },
                Status: StatusFilterIndex switch
                {
                    1 => OffenerPostenStatus.Offen,
                    2 => OffenerPostenStatus.TeilweiseBezahlt,
                    3 => OffenerPostenStatus.Ausgeglichen,
                    _ => null,
                },
                NurUeberfaellige: NurUeberfaellige);
            Posten = await _offenePostenService.ListeAsync(filter);
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("Fehler beim Laden", ex.Message);
        }
        finally
        {
            LaedtGerade = false;
        }
    }
}
