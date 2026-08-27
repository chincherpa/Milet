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
