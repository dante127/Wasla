using FluentValidation;

namespace Wasla.Conversations.Application;

public sealed class CreateTagValidator : AbstractValidator<CreateTagRequest>
{
    public CreateTagValidator()
    {
        RuleFor(request => request.Key)
            .NotEmpty()
            .Matches("^[a-z0-9][a-z0-9-]{1,62}[a-z0-9]$")
            .WithMessage("Tag key must be 3-64 characters: lowercase letters, digits and dashes.");

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(request => request.Color)
            .Matches("^#[0-9a-fA-F]{6}$")
            .When(request => !string.IsNullOrWhiteSpace(request.Color))
            .WithMessage("Color must be a hex value like #7c3aed.");
    }
}
