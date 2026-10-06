using FluentValidation;

namespace Wasla.Channels.Application;

public sealed class CreateChannelValidator : AbstractValidator<CreateChannelRequest>
{
    public CreateChannelValidator()
    {
        RuleFor(request => request.Type)
            .NotEmpty()
            .MaximumLength(20);

        RuleFor(request => request.DisplayName)
            .NotEmpty()
            .MaximumLength(150);
    }
}
