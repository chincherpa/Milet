using FluentValidation;

namespace Milet.Application.Lager;

public sealed class LagerortValidator : AbstractValidator<LagerortDto>
{
    public LagerortValidator()
    {
        RuleFor(l => l.Code).NotEmpty().MaximumLength(10);
        RuleFor(l => l.Bezeichnung).NotEmpty().MaximumLength(100);
    }
}

public sealed class BestandskorrekturValidator : AbstractValidator<BestandskorrekturDto>
{
    public BestandskorrekturValidator()
    {
        RuleFor(k => k.ArtikelId).GreaterThan(0);
        RuleFor(k => k.LagerortId).GreaterThan(0);
        RuleFor(k => k.MengeDelta).NotEqual(0m).WithMessage("Mengenänderung darf nicht 0 sein.");
        RuleFor(k => k.Grund).NotEmpty().MaximumLength(200);
    }
}
