using FluentValidation;

namespace Wasla.Tenancy.Application;

public sealed class CreateTenantValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.Slug)
            .NotEmpty()
            .Matches("^[a-z0-9](?:[a-z0-9-]{1,62}[a-z0-9])$")
            .WithMessage("Slug must be 3-64 characters: lowercase letters, digits and dashes (no leading/trailing dash).");

        RuleFor(command => command.DefaultCulture)
            .NotEmpty()
            .MaximumLength(10);

        RuleFor(command => command.TimeZone)
            .NotEmpty()
            .MaximumLength(64);
    }
}
