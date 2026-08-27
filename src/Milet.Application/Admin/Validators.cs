using FluentValidation;

namespace Milet.Application.Admin;

public sealed class FibuKonfigurationValidator : AbstractValidator<FibuKonfigurationDto>
{
    public FibuKonfigurationValidator()
    {
        RuleFor(f => f.BeraterNr).GreaterThan(0).WithMessage("Beraternummer ist erforderlich.");
        RuleFor(f => f.MandantNr).GreaterThan(0).WithMessage("Mandantennummer ist erforderlich.");
        RuleFor(f => f.WirtschaftsjahrBeginnMonat).InclusiveBetween(1, 12);
        RuleFor(f => f.SachkontenLaenge).InclusiveBetween(4, 8);
        RuleFor(f => f.BankkontoNr).GreaterThan(0).WithMessage("Bankkonto ist erforderlich.");
    }
}

public sealed class BenutzerValidator : AbstractValidator<BenutzerDto>
{
    public BenutzerValidator()
    {
        RuleFor(b => b.Benutzername).NotEmpty().MaximumLength(50);
        RuleFor(b => b.Anzeigename).NotEmpty().MaximumLength(100);
        RuleFor(b => b.Email).EmailAddress().When(b => !string.IsNullOrWhiteSpace(b.Email));
        RuleFor(b => b.RolleId).GreaterThan(0).WithMessage("Eine Rolle ist erforderlich.");
        RuleFor(b => b.NeuesPasswort)
            .NotEmpty().WithMessage("Für einen neuen Benutzer ist ein Passwort erforderlich.")
            .MinimumLength(8).WithMessage("Das Passwort muss mindestens 8 Zeichen lang sein.")
            .When(b => b.Id == 0);
        RuleFor(b => b.NeuesPasswort)
            .MinimumLength(8).WithMessage("Das Passwort muss mindestens 8 Zeichen lang sein.")
            .When(b => b.Id != 0 && !string.IsNullOrEmpty(b.NeuesPasswort));
    }
}

public sealed class RolleValidator : AbstractValidator<RolleDto>
{
    public RolleValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(50);
    }
}
