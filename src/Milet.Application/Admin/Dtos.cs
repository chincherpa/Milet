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
