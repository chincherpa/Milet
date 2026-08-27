using Milet.Application.Stammdaten;

namespace Milet.Application.Admin;

public sealed record FirmenstammDto
{
    public string Firmenname { get; init; } = string.Empty;
    public AdresseDto Adresse { get; init; } = new();
    public string? UStIdNr { get; init; }
    public string? Telefon { get; init; }
    public string? Email { get; init; }
    public string? Iban { get; init; }
    public string? Bic { get; init; }
}

public sealed record FibuKonfigurationDto
{
    public Milet.Domain.Entities.Admin.Kontenrahmen Kontenrahmen { get; init; } = Milet.Domain.Entities.Admin.Kontenrahmen.Skr03;
    public int BeraterNr { get; init; }
    public int MandantNr { get; init; }
    public int WirtschaftsjahrBeginnMonat { get; init; } = 1;
    public int SachkontenLaenge { get; init; } = 4;
    public int BankkontoNr { get; init; }
}
