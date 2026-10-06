using FluentValidation;

namespace Wasla.Conversations.Application;

public sealed class CreateQuickReplyValidator : AbstractValidator<CreateQuickReplyRequest>
{
    public CreateQuickReplyValidator()
    {
        RuleFor(request => request.Key)
            .NotEmpty()
            .Matches("^[a-z0-9][a-z0-9-]{0,62}[a-z0-9]$")
            .WithMessage("Quick reply key must be 2-64 characters: lowercase letters, digits and dashes.");

        RuleFor(request => request.Text)
            .NotEmpty()
            .MaximumLength(2000);
    }
}
