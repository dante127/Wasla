using FluentValidation;

namespace Wasla.Customers.Application;

public sealed class AddIdentityValidator : AbstractValidator<AddIdentityRequest>
{
    public AddIdentityValidator()
    {
        RuleFor(request => request.ChannelType)
            .NotEmpty()
            .MaximumLength(20);

        RuleFor(request => request.ExternalId)
            .NotEmpty()
            .MaximumLength(320);

        RuleFor(request => request.DisplayValue)
            .MaximumLength(400);
    }
}
