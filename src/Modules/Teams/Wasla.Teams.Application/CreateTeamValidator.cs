using FluentValidation;

namespace Wasla.Teams.Application;

public sealed class CreateTeamValidator : AbstractValidator<CreateTeamRequest>
{
    public CreateTeamValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(request => request.Description)
            .MaximumLength(500);
    }
}
